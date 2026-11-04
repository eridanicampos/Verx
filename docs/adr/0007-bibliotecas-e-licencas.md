---
status: accepted
date: 2026-10-29
---

# ADR-0007 — Somente bibliotecas com licença permissiva; versões fixadas

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
Em 2025 várias bibliotecas populares do ecossistema .NET passaram a ter licença comercial em versões novas (MediatR 13+, AutoMapper 15+, MassTransit 9+, FluentAssertions 8+). Ferramentas como NBomber exigem licença para uso organizacional e k6 é AGPL-3.0. Uma dependência com licença inadequada é risco jurídico para o banco, e atualização automática pode "trocar" a licença sem ninguém perceber.

## Decisão
- **Allowlist de licenças:** MIT, Apache-2.0, BSD-2/3-Clause, PostgreSQL, ISC, MS-PL. Licenças duplas são aceitas se uma opção for permitida (ex.: RabbitMQ.Client `Apache-2.0 OR MPL-2.0` — usamos sob Apache-2.0).
- **Substituições:**

| Necessidade | Evitado (licença) | Adotado |
|---|---|---|
| Mediator/CQRS | MediatR 13+ (comercial) | Interfaces `ICommandHandler`/`IQueryHandler` próprias + DI |
| Mapeamento | AutoMapper 15+ (comercial) | Métodos `From(...)` explícitos |
| Mensageria | MassTransit 9+ (comercial) | `RabbitMQ.Client` + Outbox/Inbox próprios |
| Asserções | FluentAssertions 8+ (comercial) | Shouldly (BSD-3) |
| Carga | NBomber (licença org.), k6 (AGPL) | `CashFlow.LoadTest` próprio (ADR-0015) |
| Testes | — | xUnit v3 (Apache-2.0), NSubstitute (BSD-3), Testcontainers (MIT) |

- **Fixação de versões:** Central Package Management (`Directory.Packages.props`) com versões exatas, `CentralPackageTransitivePinningEnabled` e **lock files** (`packages.lock.json`) com restore em modo travado no CI e no Docker.
- **Vulnerabilidades:** `NuGetAudit` em modo `all` (inclui **transitivas**, inclusive nos projetos de teste) com `TreatWarningsAsErrors` — uma CVE conhecida quebra o build. O CI ainda roda `dotnet list package --vulnerable --include-transitive`.
- **Verificação automatizada:** `scripts/check-licenses.py` lê todos os lock files e falha o CI se houver licença fora da allowlist. Inventário gerado em [`docs/governanca/inventario-licencas.md`](../governanca/inventario-licencas.md) (133 pacotes, 100% permissivos).
- Dependabot agrupa atualizações; toda atualização passa pelo verificador de licenças.

## Consequências
- ➕ Zero risco de licença comercial inadvertida; supply chain auditável.
- ➖ Algum código de infraestrutura próprio (mediator simples, outbox) — pequeno e coberto por testes.
