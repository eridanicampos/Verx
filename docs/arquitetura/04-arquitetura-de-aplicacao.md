# 4. Arquitetura de Aplicação (TOGAF ADM — Fase C, Aplicação)

## 4.1 Visões C4
- [Nível 1 — Contexto](../c4/c4-contexto.md)
- [Nível 2 — Containers (inclui camada de UI/Cliente)](../c4/c4-containers.md)
- [Nível 3 — Componentes](../c4/c4-componentes.md)
- Modelo formal: [`docs/c4/workspace.dsl`](../c4/workspace.dsl) (Structurizr)

## 4.2 Organização do código

```
src/
├── API/                   # 1 - Presentation
│   ├── CashFlow.Transactions.Api
│   ├── CashFlow.Consolidation.Api
│   ├── CashFlow.Identity.Api      # projeto único com pastas por camada (ADR-0008)
│   └── CashFlow.Gateway           # YARP + rate limiting
├── Application/           # 2 - Application (CQRS)
│   ├── CashFlow.Transactions.Application
│   └── CashFlow.Consolidation.Application
├── Domain/                # 3 - Domain
│   ├── CashFlow.Transactions.Domain
│   └── CashFlow.Consolidation.Domain
├── Infrastructure/        # 4 - Infra
│   ├── CashFlow.Transactions.Infrastructure
│   └── CashFlow.Consolidation.Infrastructure
├── Tests/                 # 5 - Tests
│   ├── CashFlow.Transactions.UnitTests / IntegrationTests
│   ├── CashFlow.Consolidation.UnitTests / IntegrationTests
│   ├── CashFlow.Identity.Tests
│   ├── CashFlow.Gateway.Tests
│   └── CashFlow.LoadTest          # teste de carga (console)
└── Web/cashflow-web/      # Console Web (UI) + Nginx
docker/                    # Dockerfiles (identity, transactions, consolidation, gateway)
```

A organização usa uma pasta por camada (`API`, `Application`, `Domain`, `Infrastructure`, `Tests`), e dentro de cada uma um projeto por microsserviço. Não existe projeto compartilhado entre serviços ([ADR-0004](../adr/0004-contratos-sem-projeto-compartilhado.md)). Regras de dependência: [ADR-0013](../adr/0013-clean-architecture.md).

## 4.3 Catálogo de APIs

| Método | Rota (via Gateway) | Serviço | Escopo | Observações |
|---|---|---|---|---|
| POST | `/auth/token` | Identidade | anônimo | Rate limit estrito; `useCookie` para web |
| POST | `/auth/refresh` | Identidade | refresh token | Rotação + detecção de reuso |
| POST | `/auth/revoke` | Identidade | refresh token | Logout (revoga família) |
| GET | `/.well-known/openid-configuration`, `/.well-known/jwks.json` | Identidade | anônimo | Discovery/JWKS |
| POST | `/api/v1/accounts` | Lançamentos | `transactions:write` | Documento cifrado; resposta mascarada |
| GET | `/api/v1/accounts/{id}` | Lançamentos | `transactions:read` | Saldo transacional (fonte da verdade) |
| POST | `/api/v1/accounts/{id}/transactions` | Lançamentos | `transactions:write` | **`Idempotency-Key` obrigatório**; 201/200 replay/409/422 |
| GET | `/api/v1/accounts/{id}/transactions?from&to&limit` | Lançamentos | `transactions:read` | Extrato |
| GET | `/api/v1/accounts/{id}/balance?at=` | Consolidado | `balances:read` | Saldo em qualquer instante |
| GET | `/api/v1/accounts/{id}/daily-balances?from&to` | Consolidado | `balances:read` | Até 366 dias |

Erros seguem **RFC 9457 (Problem Details)** com `code` estável:

| HTTP | Quando |
|---|---|
| 400 | Validação (`validation.failed`), header de idempotência ausente |
| 401 / 403 | Sem token/inválido · escopo insuficiente |
| 404 | Conta inexistente |
| 409 | `idempotency.key_reused`, `account.duplicate_holder`, `transaction.concurrency_conflict` |
| 422 | Regra de negócio (`account.insufficient_funds`, `account.inactive`) |
| 429 | Rate limit (com `Retry-After`) |

## 4.4 Padrões aplicados

| Padrão | Onde | Motivo |
|---|---|---|
| Clean Architecture / DIP | Serviços de negócio | Testabilidade, isolamento de tecnologia |
| Aggregate (DDD) | `Account` | Invariante de saldo em um único ponto |
| CQRS (entre serviços) | Lançamentos (escrita) × Consolidado (leitura) | Modelos otimizados para cada carga |
| Transactional Outbox | Lançamentos | Atomicidade banco + evento |
| Idempotent Consumer / Inbox | Consolidado | At-least-once sem duplicidade |
| Retry com backoff + DLQ | Consumidor | Falhas transitórias × permanentes |
| Idempotency-Key | API de escrita | Retentativas seguras do cliente |
| Optimistic Concurrency | `Account.Version` | Concorrência sem locks longos |
| Result pattern | Application/Domain | Erros de negócio explícitos |
| API Gateway / BFF de borda | YARP | Proteção e roteamento |
| Health Check (liveness × readiness) | Todos | Orquestrador distingue "morto" de "não pronto" |
| Tolerant Reader | Consumidor | Evolução de contrato sem quebra |

## 4.5 Comportamento sob falha (resumo)

| Falha | Efeito | Resultado medido/testado |
|---|---|---|
| RabbitMQ indisponível | Lançamentos seguem (outbox acumula); readiness *Degraded* | 0 erro em 600 escritas com broker fora por 18 s; convergência total |
| Consolidado indisponível | Lançamentos seguem; mensagens aguardam na fila quorum | Fila absorve; KEDA escala ao voltar |
| Banco do Consolidado lento/indisponível | Retry com atraso → DLQ após N tentativas | `RetryAndDeadLetterTests` |
| Mensagem inválida | DLQ imediata (sem retry inútil) | `Poison_message_goes_straight_to_dlq_without_retries` |
| Réplica de Lançamentos cai no meio do publish | Mensagem reenviada (at-least-once), Inbox deduplica | `Redelivered_message_is_deduplicated_by_inbox` |
| Pico de acessos/abuso | 429 no gateway/WAF; serviços protegidos | `GatewayTests` |
| Identity indisponível | Tokens já emitidos seguem válidos (validação local via JWKS em cache) | — |

Detalhes e evidências: [`07-qualidade-e-resiliencia.md`](07-qualidade-e-resiliencia.md).
