# Estratégia de Testes

## Pirâmide

| Nível | Projeto | O que cobre | Infra |
|---|---|---|---|
| Unitário | `CashFlow.Transactions.UnitTests` | Agregado `Account`, handlers (idempotência, retry de concorrência, corrida), validação, criptografia AES-GCM (rotação, adulteração), backoff do outbox | Nenhuma |
| Unitário | `CashFlow.Consolidation.UnitTests` | Política de falha (retry × DLQ), projetores (Inbox, chave natural, payload inválido), saldo em T, relatório diário | Nenhuma |
| Contrato | `ProducerContractTests` / `ConsumerContractTests` | Eventos `*.v1` contra `docs/contratos/exemplos` | Nenhuma |
| Integração | `CashFlow.Transactions.IntegrationTests` | API real + PostgreSQL + RabbitMQ: **autorização** (401/403/expirado/audiência/chave), **idempotência** (replay, 409, rajada concorrente), invariante de saldo sob concorrência, PII cifrada, **outbox** (atomicidade, broker fora, relays concorrentes), publisher confirms, mensagem sem rota | Testcontainers |
| Integração | `CashFlow.Consolidation.IntegrationTests` | Broker → consumidor → projeção → API; **duplicatas (Inbox)**, **DLQ** (poison, inválida, tipo desconhecido, sem MessageId), **retry transitório até DLQ** com contagem, recuperação dentro do orçamento, saldo em instantes, eventos fora de ordem, 100 projeções concorrentes, autorização | Testcontainers |
| Integração | `CashFlow.Identity.Tests` | Login, JWKS, escopos, anti-enumeração, lockout, rotação e **reuso de refresh token**, revogação, cookie endurecido, rate limit | Testcontainers |
| Componente | `CashFlow.Gateway.Tests` | Roteamento, rate limiting (429 + Retry-After), limite de auth, payload, bypass do Front Door, health | Kestrel in-process |
| Carga | `CashFlow.LoadTest` | 50 req/s no consolidado (≤ 5% perda), escrita concorrente, convergência | Stack completa |

**Total: 117 testes automatizados**, executados 6× consecutivas sem falha (estabilidade). Cobertura de linhas: Domain/Application/Infrastructure/Api entre 90% e 98% nos testes de integração; domínio 97% só com unitários.

## Como executar

```bash
# Unitários + contrato + integração (requer Docker para Testcontainers)
dotnet test --solution CashFlow.slnx

# Sem Docker: apontar para instâncias existentes
export CASHFLOW_TEST_POSTGRES="Host=localhost;Username=postgres;Password=..."
export CASHFLOW_TEST_RABBITMQ="amqp://user:pass@localhost:5672"
dotnet test --solution CashFlow.slnx

# Cobertura (Cobertura XML)
dotnet test --solution CashFlow.slnx --coverlet --coverlet-output-format cobertura
```

## Resultados de carga

Ambiente: execução local, 4 vCPU / 16 GB, build Release, serviços sem container, através do Gateway (com rate limiting ativo). Relatórios brutos em [`evidencias/`](evidencias).

| Cenário | Taxa alvo | Total | Erros | p50 | p95 | p99 | Consistência |
|---|---|---|---|---|---|---|---|
| Misto — leitura consolidado | 50 rps | 1.500 | **0%** | 5,7 ms | 8,8 ms | 13,4 ms | ✅ |
| Misto — escrita lançamentos | 20 rps | 600 | **0%** | 7,6 ms | 12,8 ms | 23,4 ms | convergiu em 0,9 s |
| Consolidado (capacidade) | 90 rps | 2.700 | **0%** | 5,5 ms | 7,7 ms | 9,7 ms | — |
| Escrita com **RabbitMQ derrubado por 18 s** | 20 rps | 600 | **0%** | 7,4 ms | 10,0 ms | 12,8 ms | convergiu em 16,9 s após o retorno |

No CI (`containers-e2e`) o mesmo teste roda contra a stack Docker Compose por 60 s, além de um cenário de resiliência com o broker parado; o relatório é publicado como artefato.

```bash
dotnet run -c Release --project src/Tests/CashFlow.LoadTest -- \
  --base-url http://localhost:8000 \
  --password-file secrets/identity/Identity__SeedUsers__0__Password \
  --scenario mixed --rps 50 --write-rps 20 --duration 60 \
  --max-error-rate 0.05 --p95-ms 500
```
