---
status: accepted
date: 2026-10-29
---

# ADR-0015 — Teste de carga próprio em modelo aberto (CashFlow.LoadTest)

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
Requisito não funcional de volumetria adotado na avaliação: em dias de pico o serviço de consolidado recebe **50 requisições/s com no máximo 5% de perda**. Precisamos comprovar isso de forma automatizada e repetível no CI. NBomber exige licença comercial para uso organizacional; k6 é AGPL-3.0 (ADR-0007).

## Decisão
Implementar `src/Tests/CashFlow.LoadTest` (console .NET, sem dependências):
- **Modelo aberto** (taxa de chegada constante): as requisições são disparadas no ritmo alvo independentemente da latência, evitando *coordinated omission* (que no modelo fechado mascara lentidão).
- Cenários: `consolidated` (leituras), `transactions` (escritas), `mixed` (ambos simultâneos).
- Métricas: total, RPS obtido, taxa de erro, p50/p95/p99/máx, distribuição por status HTTP.
- **Critérios de aprovação** parametrizáveis (`--max-error-rate 0.05 --p95-ms 500`) → exit code ≠ 0 reprova o pipeline.
- **Verificação de consistência**: ao final, compara o saldo consolidado com o transacional de cada conta (prova que não houve perda/duplicidade sob carga).
- Relatório Markdown salvo como artefato do CI (`loadtest-results/`).

## Resultados de referência
Ver [`docs/testes/estrategia-de-testes.md`](../testes/estrategia-de-testes.md#resultados-de-carga).

## Consequências
- ➕ Requisito comprovado a cada execução do pipeline, sem custo de licença.
- ➖ Recursos avançados (rampas, distribuição de carga em vários agentes) teriam de ser implementados; para escala maior, Azure Load Testing (JMeter, Apache-2.0) é a evolução.
