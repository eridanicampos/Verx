# Documentação de Arquitetura — CashFlow

Organizada segundo as fases do **TOGAF ADM**, com visões **C4** e registro de decisões (**ADR**).

| Fase TOGAF | Documento |
|---|---|
| Preliminar / Governança | [Princípios e governança](arquitetura/09-governanca.md) · [ADRs](adr/README.md) |
| A — Visão | [01 · Visão arquitetural](arquitetura/01-visao-arquitetural.md) (stakeholders, objetivos, requisitos, princípios) |
| B — Negócio | [02 · Arquitetura de negócio](arquitetura/02-arquitetura-de-negocio.md) (capacidades, contextos DDD, processos, regras) |
| C — Dados | [03 · Arquitetura de dados](arquitetura/03-arquitetura-de-dados.md) (modelos, propriedade, consistência, retenção) |
| C — Aplicação | [04 · Arquitetura de aplicação](arquitetura/04-arquitetura-de-aplicacao.md) · [C4 L1](c4/c4-contexto.md) · [C4 L2](c4/c4-containers.md) · [C4 L3](c4/c4-componentes.md) · [Structurizr DSL](c4/workspace.dsl) |
| D — Tecnologia | [05 · Arquitetura de tecnologia](arquitetura/05-arquitetura-de-tecnologia.md) (local, nuvem, HA/DR, escala) |
| Transversal | [06 · Segurança](arquitetura/06-seguranca.md) · [07 · Qualidade e resiliência](arquitetura/07-qualidade-e-resiliencia.md) |
| E/F — Oportunidades e migração | [08 · Roadmap e migração do legado](arquitetura/08-oportunidades-e-migracao.md) |
| G/H — Governança e mudanças | [09 · Governança](arquitetura/09-governanca.md) · [Licenças](governanca/licencas.md) |

Outros artefatos:
- **Contratos:** [AsyncAPI](contratos/asyncapi.yaml) · [exemplos](contratos/exemplos) · OpenAPI ([Lançamentos](contratos/openapi-transactions.json), [Consolidado](contratos/openapi-consolidation.json), [Identidade](contratos/openapi-identity.json))
- **Testes:** [estratégia e resultados de carga](testes/estrategia-de-testes.md)
- **FinOps:** [estimativa de custos](finops/estimativa-custos.md) (gerada de [`infra/finops/cost-model.csv`](../infra/finops/cost-model.csv))
- **Operação:** [runbooks](operacao/runbooks.md)
