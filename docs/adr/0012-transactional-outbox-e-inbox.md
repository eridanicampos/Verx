---
status: accepted
date: 2026-10-29
---

# ADR-0012 — Transactional Outbox no produtor e Inbox no consumidor

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
O problema de *dual write*: gravar o lançamento no banco **e** publicar o evento no broker não é atômico. Se publicar antes do commit, o consumidor pode ver um evento de algo que não existiu (rollback); se depois, uma queda entre commit e publish perde o evento — o consolidado diverge para sempre.

## Decisão
### Produtor (Lançamentos) — Transactional Outbox
1. O caso de uso grava `Account` (saldo), `Transaction` e uma linha em `outbox_messages` **no mesmo `SaveChanges`** (uma transação).
2. O `OutboxProcessor` (BackgroundService) reserva lotes com `SELECT … FOR UPDATE SKIP LOCKED` (várias réplicas sem duplicar trabalho), publica com **publisher confirms** e marca `processed_at`.
3. Falha de publicação → `attempts++`, backoff exponencial (2^n s, máx. 5 min). Após 20 tentativas a mensagem fica "estacionada" e o health check `outbox` reporta *Degraded* (alerta).
4. Mensagens processadas são removidas após 7 dias (retenção configurável).
5. `MessageId` AMQP = `EventId` (UUIDv7), propagado com `traceparent` (W3C) para rastreamento ponta a ponta.

### Consumidor (Consolidado) — Inbox / Idempotent Consumer
1. Ack manual **somente após o commit** da projeção.
2. Na mesma transação: `INSERT INTO inbox_messages … ON CONFLICT DO NOTHING`; se já existia, é duplicata → ack sem efeito.
3. Segunda barreira: `ledger_entries` tem PK = `TransactionId` (idempotência natural mesmo se o evento for republicado com outro `MessageId`).
4. Falha transitória → republica na fila de retry (TTL) com `x-retry-count` e confirma a original **depois** do confirm do retry; esgotado → DLQ.

### Idempotência na borda (API)
`POST /transactions` exige header `Idempotency-Key`. Unicidade `(account_id, idempotency_key)` no banco; repetição com o mesmo payload retorna o resultado original (`200` + `Idempotency-Replayed: true`); com payload diferente retorna `409`. Corridas entre réplicas são resolvidas pela constraint.

## Consequências
- ➕ Garantia *at-least-once* fim a fim + efeito *exactly-once* na projeção.
- ➕ Escrita continua disponível com o broker fora (testado: 600 lançamentos durante 18 s de indisponibilidade, 0 erro, convergência total).
- ➖ Latência adicional de até `PollingInterval` (500 ms) para o evento sair; aceitável para consolidado. Evolução: LISTEN/NOTIFY ou CDC (Debezium) para reduzir.
- ➖ Tabelas de controle (outbox/inbox) exigem limpeza periódica (implementada no outbox; inbox via job de retenção).
- Testes: `OutboxTests` (atomicidade, broker fora, relays concorrentes), `MessagingTests` (duplicatas, DLQ), `IdempotencyTests` (rajada concorrente com a mesma chave).
