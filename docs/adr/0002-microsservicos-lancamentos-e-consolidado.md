---
status: accepted
date: 2026-10-29
---

# ADR-0002 — Dois microsserviços de negócio com database-per-service

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
O problema tem duas capacidades com perfis opostos:

| | Lançamentos | Consolidado |
|---|---|---|
| Natureza | Escrita, transacional, fonte da verdade | Leitura intensiva, derivada |
| Consistência | Forte (saldo não pode ficar inconsistente) | Eventual aceitável (com indicação de defasagem) |
| Pico | Horário comercial | Fechamentos e relatórios |
| Requisito crítico | Não pode parar se o consolidado cair | Suportar 50 req/s com ≤ 5% de perda |

No legado, ambos competem pelos mesmos recursos: relatórios pesados degradam o registro de movimentações em horário de pico.

## Decisão
Separar em **dois serviços independentemente implantáveis e escaláveis** (Lançamentos e Consolidado), cada um com **seu próprio banco** (database-per-service) e comunicação **assíncrona por eventos**. Identidade e Gateway são serviços de suporte.

- Lançamentos não chama o Consolidado de forma síncrona — falha do Consolidado não afeta o registro.
- O Consolidado mantém uma **projeção própria** (CQRS entre serviços), otimizada para leitura temporal.
- Em produção os bancos ficam no mesmo servidor PostgreSQL gerenciado (isolamento lógico por database e usuário) para reduzir custo; a separação física é uma mudança de connection string.

## Alternativas consideradas
1. **Monólito modular** (um deploy, dois módulos, um banco): mais simples e barato, mas não atende ao requisito de isolamento de falha e escala independente; o problema de contenção do legado persistiria. Seria a escolha para um time muito pequeno sem os requisitos de disponibilidade.
2. **Mais microsserviços** (contas, lançamentos, relatórios, notificações…): granularidade excessiva para o domínio atual, aumenta custo operacional e risco de monólito distribuído.
3. **Consolidado lendo o banco de Lançamentos** (réplica de leitura): acopla o modelo de dados e transfere a carga de relatório para o banco transacional.

## Consequências
- ➕ Isolamento de falhas e escala independente (o Consolidado escala por profundidade de fila via KEDA).
- ➕ Cada serviço evolui seu modelo de dados sem coordenação.
- ➖ Consistência eventual entre serviços (tratada no ADR-0006) e necessidade de Outbox/Inbox (ADR-0012).
- ➖ Mais artefatos de operação (observabilidade distribuída com OpenTelemetry é obrigatória).
- Para evitar **monólito distribuído**: sem chamadas síncronas entre serviços de negócio, sem código compartilhado em tempo de compilação (ADR-0004), deploy independente por serviço.
