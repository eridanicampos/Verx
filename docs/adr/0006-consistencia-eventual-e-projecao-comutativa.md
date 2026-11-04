---
status: accepted
date: 2026-10-29
---

# ADR-0006 — Consistência eventual no consolidado com projeção comutativa

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
A posição consolidada é derivada dos lançamentos. Exigir consistência forte entre os serviços (2PC/saga síncrona) acoplaria a disponibilidade de Lançamentos à do Consolidado. O broker entrega *at-least-once* e não garante ordem entre consumidores concorrentes.

## Decisão
1. **Fonte da verdade:** o serviço de Lançamentos mantém o saldo transacional com consistência forte (concorrência otimista no agregado `Account`, com retentativas limitadas). `GET /api/v1/accounts/{id}` retorna esse saldo.
2. **Consolidado eventualmente consistente**, com a defasagem **explícita** na resposta (`lastEntryAt`, `consistency: "eventual"`). Em condições normais a convergência medida é < 1 s; com o broker fora por 18 s, convergiu em ~17 s após o retorno, sem perda.
3. **Projeção comutativa e idempotente:**
   - Cada lançamento vira uma `LedgerEntry` com chave = `TransactionId` (reaplicar não altera nada).
   - O consolidado diário é atualizado com `INSERT … ON CONFLICT DO UPDATE SET total = total + EXCLUDED.total` — **soma atômica**, sem read-modify-write, segura com N consumidores.
   - Como somas comutam, eventos **fora de ordem** produzem o mesmo resultado; dispensa ordenação global e permite consumidores paralelos.
4. **Saldo em um instante T** = Σ consolidados diários anteriores ao dia de T + Σ lançamentos do dia de T até T. Custo proporcional a (dias de histórico) + (lançamentos do dia), não ao histórico completo.
5. **Data contábil em UTC** (`BusinessCalendar`). O cliente converte para America/Sao_Paulo na apresentação. Se o negócio exigir fechamento no fuso local, a mudança fica isolada em `BusinessCalendar` + reprocessamento da projeção.

## Alternativas consideradas
- **Calcular saldo sob demanda no banco transacional:** simples, mas leva a carga de relatório para o caminho crítico de escrita (problema do legado).
- **Saldo corrente com "running balance" por lançamento:** exige ordem estrita por conta; quebra com eventos fora de ordem.
- **Event sourcing completo:** poderoso para auditoria/replay, mas complexidade desproporcional ao escopo.

## Consequências
- ➕ Lançamentos disponíveis mesmo com Consolidado/broker indisponíveis.
- ➕ Consumidores escalam horizontalmente sem coordenação.
- ➖ Leituras do consolidado podem estar defasadas por segundos; clientes que precisam do valor exato e imediato usam o saldo transacional.
- Testes: `Out_of_order_events_yield_same_result`, `Concurrent_projections_on_same_account_do_not_lose_updates`, verificação de convergência no `CashFlow.LoadTest`.
