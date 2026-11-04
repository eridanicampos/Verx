---
status: accepted
date: 2026-10-29
---

# ADR-0010 — Runtime .NET 10 LTS

- **Status:** Aceito (substitui a escolha anterior por .NET 8)
- **Data:** 2026-10-29

## Contexto
Uma versão anterior da solução usava .NET 8. Ciclo de vida oficial da Microsoft:

| Versão | Tipo | Lançamento | Fim do suporte |
|---|---|---|---|
| .NET 8 | LTS | nov/2023 | **10/nov/2026** |
| .NET 9 | STS | nov/2024 | 10/nov/2026 |
| **.NET 10** | **LTS** | **nov/2025** | **nov/2028** |

Na data desta decisão o .NET 8 está **a menos de duas semanas do fim do suporte**: depois disso não recebe correções de segurança — inaceitável para um sistema financeiro (e apontamento certo em auditoria/pentest).

## Decisão
- Todos os projetos usam **`net10.0`** (C# 14), SDK fixado em `global.json` (`10.0.100`, `rollForward: latestFeature`).
- Imagens base `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` (patches de segurança via rebuild; Dependabot monitora as tags).
- Bibliotecas Microsoft na linha 10.0.x (EF Core 10, ASP.NET Core 10).
- **Política de evolução:** adotar cada LTS em até 6 meses após o GA; nunca operar em versão a menos de 6 meses do fim do suporte. A próxima migração planejada é para o .NET 12 LTS (nov/2027), com janela até mai/2028.

## Alternativas consideradas
- **Manter .NET 8 com justificativa:** só seria defensável com plano de migração imediato; o custo de migrar (8 → 10) foi baixo (sem APIs removidas relevantes) e o risco de ficar sem patches é alto.
- **.NET 9 (STS):** mesmo fim de suporte do .NET 8 — sem vantagem.

## Benefícios técnicos observados
- `Guid.CreateVersion7` (IDs ordenáveis por tempo, melhor localidade de índice) usado em todas as entidades.
- Melhorias de performance do runtime/GC e do Kestrel; OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`) para controllers e Minimal APIs.
- `KnownIPNetworks` em `ForwardedHeadersOptions`, `TimeProvider`/`FakeTimeProvider` para testes determinísticos.

## Consequências
- ➕ Suporte de segurança até nov/2028.
- ➖ Algumas bibliotecas de terceiros ainda sem build específico para 10 (nenhuma usada aqui).
