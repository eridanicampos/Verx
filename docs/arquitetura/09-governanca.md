# 9. Governança de Arquitetura (TOGAF ADM — Fases G/H)

## 9.1 Conformidade automatizada (fitness functions)

| Regra | Como é verificada | Onde |
|---|---|---|
| Compila sem erros **e sem warnings** | `TreatWarningsAsErrors` + analisadores `latest-recommended` + `EnforceCodeStyleInBuild` | `Directory.Build.props`, CI |
| Sem vulnerabilidades conhecidas (inclui transitivas e projetos de teste) | `NuGetAudit` (`all`) + `dotnet list package --vulnerable --include-transitive` | Build, CI |
| Somente licenças permissivas | `scripts/check-licenses.py` (falha o job) | CI `licenses` |
| Versões fixadas e reprodutíveis | CPM + `packages.lock.json` + `--locked-mode` | Build, Docker, CI |
| Contratos de eventos compatíveis | Testes de contrato produtor/consumidor | CI |
| Requisito de volumetria | `CashFlow.LoadTest` com limites | CI `containers-e2e` |
| IaC válido e formatado | `terraform fmt -check` + `validate` | CI `terraform` |
| Sem acoplamento de compilação entre serviços | Não existe projeto compartilhado; Domain sem dependências | Revisão + estrutura |
| Decisões registradas | ADR no mesmo PR da mudança | Revisão de código |

## 9.2 Gestão de mudanças de arquitetura
- Mudança estrutural → ADR em `Proposto` → revisão (arquitetura + segurança + time) → `Aceito` → implementação no mesmo PR.
- Dependabot semanal (NuGet, Docker, Actions) e mensal (Terraform), agrupado por família.
- Política de runtime: migrar para cada .NET LTS em até 6 meses do GA (ADR-0010).
- Revisão trimestral de custos: real (Cost Management, por tag) × modelo (`infra/finops/cost-model.csv`).

## 9.3 Licenças
Política e inventário: [`docs/governanca/licencas.md`](../governanca/licencas.md) e [`docs/governanca/inventario-licencas.md`](../governanca/inventario-licencas.md).
