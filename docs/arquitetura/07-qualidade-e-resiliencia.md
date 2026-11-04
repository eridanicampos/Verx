# 7. Qualidade, Resiliência e Observabilidade

## 7.1 Cenários de qualidade (ATAM)

| Atributo | Estímulo | Resposta esperada | Evidência |
|---|---|---|---|
| Disponibilidade | Broker cai durante pico de escrita | Lançamentos continuam 201; eventos publicados ao voltar | Carga com RabbitMQ parado 18 s: 600/600 OK, convergência em 16,9 s |
| Desempenho | 50 req/s no consolidado + 20 escritas/s | ≤ 5% erro, p95 < 500 ms | 0% erro; p95 8,8 ms (leitura) e 12,8 ms (escrita) |
| Consistência | 30 débitos concorrentes na mesma conta | Saldo nunca abaixo do limite; ledger = saldo | `Parallel_debits_never_break_balance_invariant` |
| Idempotência | 20 requisições paralelas com a mesma chave | Exatamente 1 efeito | `Concurrent_requests_with_same_key_produce_exactly_one_effect` |
| Recuperabilidade | Mensagem envenenada | DLQ sem bloquear a fila | `Poison_message_goes_straight_to_dlq_without_retries` |
| Segurança | Token expirado / outra audiência / outra chave | 401 | `AuthorizationTests` |
| Segurança | Força bruta no login | 429 + lockout 423 | `LoginRateLimitTests`, `Repeated_failures_lock_the_account…` |
| Manutenibilidade | Mudança no contrato do evento | Build falha antes do deploy | `ProducerContractTests` / `ConsumerContractTests` |

## 7.2 Observabilidade
- **Logs** estruturados em JSON (console → coletor), com `TraceId/SpanId` em escopo.
- **Traces** OpenTelemetry (ASP.NET Core, HttpClient, Npgsql, Outbox, Consumer) exportados via OTLP; o `traceparent` W3C viaja no header AMQP, ligando a requisição HTTP ao processamento no Consolidado.
- **Métricas**: `cashflow.outbox.published/failed`, `cashflow.consumer.processed/duplicates/retried/deadlettered`, métricas de runtime e HTTP.
- **Health checks**: `/health/live` (processo) e `/health/ready` (PostgreSQL, RabbitMQ, backlog do outbox, consumidor ativo). Dependência de mensageria fora → *Degraded* (não derruba o pod).
- Local: `docker compose --profile observability up` sobe Jaeger (UI em http://localhost:16686).

### Alertas recomendados (SLO-based)
| Alerta | Condição |
|---|---|
| Outbox parado | mensagens pendentes > 2 min ou estacionadas > 0 |
| DLQ | `deadlettered` > 0 em 5 min |
| Defasagem do consolidado | profundidade da fila > 5.000 por 5 min |
| Erro de API | taxa 5xx > 1% em 5 min (burn rate do SLO 99,95%) |
| Banco | CPU > 80% por 15 min (provisionado no Terraform) |

Estratégia de testes completa: [`docs/testes/estrategia-de-testes.md`](../testes/estrategia-de-testes.md).
