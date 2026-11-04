# Resultado do teste de carga — 2026-10-29 20:34 UTC

Cenário **Consolidated**, alvo http://localhost:5000/, duração 30s, 20 contas.

| Operação | Total | RPS obtido | Erros | p50 ms | p95 ms | p99 ms | máx ms | Status HTTP |
|---|---|---|---|---|---|---|---|---|
| GET /balance (consolidado) | 2700 | 90.0 | 0.00 % | 5.5 | 7.7 | 9.7 | 49.4 | 200:2700 |

## Critérios
- GET /balance (consolidado): erro 0.00 % (limite 5 %) ✅ · p95 7.7 ms (limite 500 ms) ✅
- Consistência eventual: não aplicável ✅

**Resultado: APROVADO**
