# 1. Visão Arquitetural (TOGAF ADM — Fase A)

## 1.1 Contexto do problema
O banco digital possui um sistema interno legado que registra movimentações financeiras (créditos e débitos) e fornece, sob demanda, a posição consolidada de saldo dos clientes. O sistema apresenta **lentidão e instabilidade em horários de pico** e **dificuldade de manutenção**. A solução deve ser repensada do zero para operar de forma confiável em ambiente financeiro real.

## 1.2 Stakeholders e preocupações

| Stakeholder | Preocupação principal | Como a arquitetura responde |
|---|---|---|
| Diretoria de Operações | Disponibilidade no pico, sem perda de lançamentos | Serviços isolados, Outbox, escala automática |
| Clientes do banco | Saldo correto, dados protegidos | Invariante de saldo com concorrência otimista; PII cifrada |
| Segurança da Informação | Credenciais, ataques, LGPD | Key Vault/secrets, JWT RS256, WAF, rate limiting, criptografia de campo |
| Compliance / Auditoria | Rastreabilidade, retenção | Lançamentos imutáveis, ADRs, logs estruturados com trace |
| Engenharia | Manutenibilidade, testabilidade | Clean Architecture, 117 testes, CI com quality gates |
| Finanças (FinOps) | Custo previsível | Modelo de custos versionado, orçamentos e tags no IaC |

## 1.3 Direcionadores de negócio e objetivos

| Objetivo | Métrica (SLO proposto) |
|---|---|
| Registrar lançamentos sem depender do consolidado | Disponibilidade de Lançamentos ≥ 99,95% mensal |
| Consultar saldo consolidado em pico | 50 req/s com ≤ 5% de perda; p95 < 200 ms |
| Consistência entre transacional e consolidado | Convergência p99 < 5 s em operação normal; 0 divergência após convergência |
| Nenhuma perda de lançamento sob falha parcial | RPO = 0 para lançamentos confirmados (commit síncrono + HA do banco) |
| Recuperação | RTO < 15 min (failover automático do banco ≈ 1–2 min) |

## 1.4 Escopo
**Dentro:** abertura de conta (mínimo necessário), registro de crédito/débito, extrato, saldo transacional, saldo consolidado em qualquer instante, relatório de consolidado diário, autenticação/autorização, operação local e IaC de nuvem.

**Fora (roadmap):** estornos com workflow de aprovação, transferências entre contas (saga), multi-moeda, integração com core bancário/SPI, MFA/SSO corporativo.

## 1.5 Princípios de arquitetura

| # | Princípio | Implicação |
|---|---|---|
| P1 | **Dados financeiros nunca se perdem nem se duplicam** | Outbox/Inbox, idempotência em todas as escritas, lançamentos append-only |
| P2 | **Falhas parciais não se propagam** | Comunicação assíncrona entre serviços de negócio; health checks degradam em vez de derrubar |
| P3 | **Segurança por padrão** | Endpoints protegidos por política fallback; segredos fora do código; menor privilégio |
| P4 | **Serviços autônomos** | Database-per-service; sem projeto compartilhado; deploy independente |
| P5 | **Decisões registradas** | Toda decisão estrutural tem ADR |
| P6 | **Automatizar a verificação** | Build sem warnings, testes, licenças, vulnerabilidades e carga no CI |
| P7 | **Custo é requisito** | Estimativa versionada, orçamento e tags no IaC |

## 1.6 Requisitos

### Funcionais
| ID | Requisito |
|---|---|
| RF01 | Registrar crédito em uma conta |
| RF02 | Registrar débito respeitando saldo + limite |
| RF03 | Consultar saldo transacional atual da conta |
| RF04 | Consultar posição consolidada em um instante arbitrário (`?at=`) |
| RF05 | Relatório de consolidado diário (créditos, débitos, líquido, saldo de fechamento) |
| RF06 | Extrato de lançamentos por período |
| RF07 | Autenticar usuários e autorizar por perfil (operador × analista) |

### Não funcionais
| ID | Atributo | Requisito |
|---|---|---|
| RNF01 | Disponibilidade | Lançamentos não pode depender do Consolidado |
| RNF02 | Desempenho | Consolidado: 50 req/s, ≤ 5% de perda |
| RNF03 | Consistência | Saldo transacional fortemente consistente; consolidado eventual e explícito |
| RNF04 | Idempotência | Reenvio de requisição/evento não gera efeito duplicado |
| RNF05 | Segurança | Sem credenciais em texto puro; tokens de curta duração; proteção ativa (rate limit/WAF); PII cifrada |
| RNF06 | Observabilidade | Logs estruturados JSON, traces distribuídos (W3C), métricas de outbox/consumidor |
| RNF07 | Manutenibilidade | Clean Architecture, cobertura de testes > 85% nas camadas de negócio |
| RNF08 | Portabilidade | Executável localmente com um comando (`docker compose up`) |
| RNF09 | Conformidade | LGPD (minimização, criptografia), residência de dados no Brasil |

## 1.7 Restrições e premissas
- C#/.NET obrigatório; código público no GitHub; build sem erros **e sem warnings**.
- Premissa: moeda única BRL; volume inicial de ~16 M lançamentos/mês; nuvem Azure (ecossistema Microsoft do cliente).

## 1.8 Visão da solução
Ver [C4 Contexto](../c4/c4-contexto.md) e [C4 Containers](../c4/c4-containers.md). Em resumo: **Console Web → Front Door/WAF → API Gateway → {Identidade, Lançamentos, Consolidado}**, com Lançamentos publicando eventos via Outbox no RabbitMQ e o Consolidado projetando saldos de forma idempotente.
