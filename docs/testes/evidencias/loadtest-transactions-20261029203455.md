# Resultado do teste de carga — 2026-10-29 20:34 UTC

Cenário **Transactions**, alvo http://localhost:5000/, duração 30s, 20 contas.

| Operação | Total | RPS obtido | Erros | p50 ms | p95 ms | p99 ms | máx ms | Status HTTP |
|---|---|---|---|---|---|---|---|---|
| POST /transactions | 600 | 20.0 | 0.00 % | 7.4 | 10.0 | 12.8 | 15.6 | 201:600 |

## Critérios
- POST /transactions: erro 0.00 % (limite 5 %) ✅ · p95 10.0 ms (limite 500 ms) ✅
- Consistência eventual: OK — consolidado convergiu para o saldo transacional em 16.9s ✅

**Resultado: APROVADO**
