---
status: accepted
date: 2026-10-29
---

# ADR-0003 — RabbitMQ como broker de eventos

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
Precisamos propagar eventos de lançamento para o Consolidado com: entrega garantida (at-least-once), tratamento de falhas com retry e DLQ, operação local idêntica à de produção (Docker Compose) e custo baixo para o volume previsto (dezenas de eventos/s, picos de centenas).

## Decisão
Usar **RabbitMQ 4.1** com:
- Exchange `cashflow.transactions` (topic) de propriedade do produtor; routing key = tipo do evento versionado (`transaction.registered.v1`).
- Fila **quorum** `consolidation.transactions` de propriedade do consumidor (replicação Raft, segura contra perda de nó).
- **Publisher confirms** + `mandatory`: o Outbox só marca como publicado após confirmação do broker; mensagem sem rota retorna erro e é retentada.
- **Retry com atraso** via fila de espera com TTL que devolve à fila principal; após `MaxRetries`, `nack(requeue=false)` → **DLX/DLQ** com header `x-death`.
- Erros determinísticos (payload inválido, tipo desconhecido) vão **direto** para a DLQ.
- Cliente oficial `RabbitMQ.Client` 7.x (Apache-2.0/MPL-2.0 dual), sem framework de terceiros.

Fase 1 em nuvem: nó único no Azure Container Apps com volume persistente (o Outbox torna uma indisponibilidade do broker não-fatal para escrita). **Alvo de produção:** cluster de 3 nós (operador RabbitMQ no AKS ou CloudAMQP dedicado) para quorum real.

## Alternativas consideradas
| Opção | Prós | Contras |
|---|---|---|
| **Azure Service Bus Premium** | Gerenciado, DLQ nativa, sessões | Sem emulação local fiel; ~US$ 680/mês por unidade de mensageria Premium; lock-in |
| **Apache Kafka / Event Hubs** | Retenção e replay, altíssimo throughput | Superdimensionado para o volume; operação e custo maiores; DLQ não nativa |
| **MassTransit** sobre RabbitMQ | Abstrações prontas (outbox, retry) | v9 passou a ter licença comercial — ver ADR-0007 |
| **Somente banco (polling entre serviços)** | Sem broker | Acoplamento por banco, sem fan-out |

## Consequências
- ➕ Paridade local/produção, DLQ e retry explícitos e testados (`RetryAndDeadLetterTests`).
- ➕ Portável entre nuvens.
- ➖ Operação do broker é nossa responsabilidade (mitigado pelo plano de cluster gerenciado).
- ➖ Sem replay de longo prazo: a reconstrução do consolidado usa o banco de Lançamentos como fonte (runbook em `docs/operacao`).
