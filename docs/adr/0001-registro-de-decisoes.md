---
status: accepted
date: 2026-10-29
---

# ADR-0001 — Registrar decisões arquiteturais em ADRs versionados

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
O sistema legado "cresceu sem planejamento" e o conhecimento das decisões se perdeu, o que dificulta a manutenção. O banco exige rastreabilidade de decisões técnicas (auditoria, comitês de arquitetura).

## Decisão
Toda decisão com impacto estrutural (tecnologia, padrão, trade-off de qualidade) é registrada em `docs/adr` no formato Nygard e revisada no mesmo Pull Request que a implementa. Os ADRs são o artefato de "Architecture Requirements/Decision Log" do repositório de arquitetura TOGAF.

## Alternativas consideradas
- **Wiki/Confluence:** desacopla documentação do código, envelhece rápido e não passa por revisão de código.
- **Sem registro formal:** é justamente o problema do legado.

## Consequências
- ➕ Histórico auditável e revisado junto do código; novos membros entendem o "porquê".
- ➖ Disciplina adicional em cada mudança relevante.
