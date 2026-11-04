# Política de Licenças de Dependências

**Objetivo:** garantir que todas as bibliotecas sejam livres para uso comercial e que nenhuma atualização introduza silenciosamente uma licença restritiva. Decisão em [ADR-0007](../adr/0007-bibliotecas-e-licencas.md).

## Licenças permitidas
MIT · Apache-2.0 · BSD-2-Clause · BSD-3-Clause · PostgreSQL · ISC · MS-PL · Zlib · 0BSD — e expressões duplas em que ao menos uma opção seja permitida (ex.: `Apache-2.0 OR MPL-2.0`).

## Não permitidas sem aprovação do jurídico
GPL/AGPL/LGPL, SSPL, BUSL (em bibliotecas), licenças comerciais/"source available", "Community License" com limite de receita.

## Bibliotecas evitadas por mudança de licença

| Biblioteca | Versão com licença comercial | Alternativa adotada |
|---|---|---|
| MediatR | 13.0+ | Handlers próprios + DI |
| AutoMapper | 15.0+ | Mapeamento explícito |
| MassTransit | 9.0+ | RabbitMQ.Client + Outbox/Inbox próprios |
| FluentAssertions | 8.0+ | Shouldly |
| NBomber | (licença para organizações) | CashFlow.LoadTest |
| k6 | AGPL-3.0 | CashFlow.LoadTest |
| Duende IdentityServer | licença comercial | Identity próprio / OpenIddict (futuro) |

## Processo
1. Novo pacote → verificar licença no nuget.org → adicionar versão exata em `Directory.Packages.props`.
2. `dotnet restore` atualiza os lock files.
3. CI executa `scripts/check-licenses.py`; licença fora da lista falha o pipeline.
4. Inventário atualizado com `python3 scripts/check-licenses.py --report docs/governanca/inventario-licencas.md`.

## Imagens e ferramentas (não distribuídas com o código)
PostgreSQL (PostgreSQL License), RabbitMQ (MPL-2.0), Nginx (BSD-2), Jaeger (Apache-2.0), .NET runtime images (MIT), Terraform CLI (BUSL-1.1 — uso interno permitido; OpenTofu MPL-2.0 é alternativa compatível).
