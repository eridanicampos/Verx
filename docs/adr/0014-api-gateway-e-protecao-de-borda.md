---
status: accepted
date: 2026-10-29
---

# ADR-0014 — API Gateway (YARP) + Front Door/WAF como proteção de borda

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
A avaliação anterior apontou ausência de proteção ativa contra ataques (rate limiting, WAF). A gestão de APIs do banco é feita no **Apigee**, e o CashFlow precisa se encaixar nele sem retrabalho. Precisamos de defesa em profundidade sem acoplar regras de borda aos serviços, e de um ambiente local que reproduza essa proteção.

## Decisão
Defesa em camadas:

| Camada | Controle |
|---|---|
| **Front Door Premium + WAF** (nuvem) | TLS 1.2+, OWASP **DRS 2.1**, **Bot Manager**, rate limit por IP (3.000/min), regra específica anti-força-bruta em `/auth/token` (50/5 min), geo-filtro (BR em prod), logs para Log Analytics |
| **API Gateway — YARP** | Roteamento por rota, **rate limiting global por IP** (token bucket) e **política estrita em `/auth`**, limite de payload (64 KB) e de headers, CORS restrito, cabeçalhos de segurança, health checks passivos dos destinos, **rejeita tráfego sem `X-Azure-FDID`** do perfil (impede bypass do WAF) |
| **Serviços** | JWT + escopos, rate limiting por cliente autenticado (`sub`), validação de entrada, limites do Kestrel |
| **Identity** | Rate limit no login por IP + lockout por usuário |

YARP (MIT, Microsoft) foi escolhido por ser .NET, leve, configurável por `appsettings`/variáveis e testável in-process.

### Integração com o Apigee
O Apigee entra **na frente** da cadeia como camada de gestão de APIs; o YARP continua como gateway do domínio, e os serviços não mudam.

```
Consumidor → Apigee (proxy CashFlow) → Front Door + WAF → YARP → serviços
```

| Responsabilidade | Apigee | YARP / serviços |
|---|---|---|
| Identificação do consumidor | API products, apps e chaves (`VerifyAPIKey`) ou OAuth (`OAuthV2`/`VerifyJWT`) | JWT do Identity e escopos por endpoint |
| Proteção contra pico | `SpikeArrest` por proxy | Rate limiting por IP (token bucket) e por `sub` |
| Cota comercial | `Quota` por app/produto | — |
| Ameaças em payload | `JSONThreatProtection`, `RegularExpressionProtection` | Limite de payload e headers, validação de entrada |
| Catálogo e onboarding | Portal de desenvolvedor com o OpenAPI de [`docs/contratos`](../contratos) | — |
| Observabilidade | Apigee Analytics (consumo por app, latência, erros) | OpenTelemetry → Log Analytics |

- **Topologia:** Apigee X (gerenciado no Google Cloud) chamando o Front Door por HTTPS com mTLS, ou **Apigee hybrid** com o runtime no AKS, na mesma rede Azure dos serviços. Com o hybrid, a trava `X-Azure-FDID` do YARP passa a aceitar também o certificado de cliente do Apigee.
- **Contratos:** os proxies do Apigee são gerados a partir dos OpenAPI já versionados ([`openapi-transactions.json`](../contratos/openapi-transactions.json), [`openapi-consolidation.json`](../contratos/openapi-consolidation.json)).
- **Sem duplicar regra de negócio:** o Apigee aplica políticas de consumo; autorização fina (escopo, conta) continua nos serviços.

## Alternativas consideradas
- **Apigee como único gateway (sem YARP):** concentraria tudo no Apigee, mas não há Apigee no ambiente local nem nos testes automatizados; perderíamos a paridade local e os testes de borda in-process (`GatewayTests`).
- **Azure API Management:** equivalente funcional no ecossistema Azure, mas fora do padrão de gestão de APIs do banco; teria uma segunda plataforma de APIs para operar e governar.
- **Somente WAF, sem gateway:** perderia rate limiting por rota e não funcionaria localmente.
- **Ocelot:** projeto com manutenção menos ativa que o YARP.

## Consequências
- ➕ Proteção ativa testada (`GatewayTests`: 429 com `Retry-After`, limite de auth, payload grande, bypass do Front Door).
- ➕ Paridade local (YARP) e nuvem (Front Door + YARP), pronta para receber o Apigee na frente sem mudar os serviços.
- ➖ Um salto de rede adicional (~1 ms) pelo YARP, mais o salto do Apigee quando ele entrar.
- ➖ Rate limiting em duas camadas (Apigee e YARP) exige limites coerentes entre elas: o Apigee aplica a cota por consumidor e o YARP fica como proteção de último nível.
