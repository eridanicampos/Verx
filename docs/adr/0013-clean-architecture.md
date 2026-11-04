---
status: accepted
date: 2026-10-29
---

# ADR-0013 — Clean Architecture com CQRS, Repository e Unit of Work

- **Status:** Aceito
- **Data:** 2026-10-29

## Contexto
O legado é difícil de manter porque mistura regras de negócio com infraestrutura. Precisamos de testabilidade (regras sem banco nem broker), de liberdade para trocar tecnologia e de convenções claras sobre onde fica cada coisa. A escolha desta arquitetura — Clean Architecture com CQRS, Repository e Unit of Work — é minha.

## Decisão
Cada serviço de negócio (Lançamentos e Consolidado) tem quatro projetos, agrupados na solução nas pastas abaixo:

| Pasta da solução | Projeto | Contém |
|---|---|---|
| **1 - Presentation** | `src/API/*.Api` | `Controllers/` (um por recurso, herdando `ApiControllerBase`), `Configuration/` (`ApiConfig` com `AddApiConfig`/`UseApiConfig`, autenticação, rate limiting, observabilidade), `Middleware/`, `Contracts/` (requests HTTP) |
| **2 - Application** | `src/Application/*.Application` | `Abstractions/Messaging/` (`ICommand`, `ICommandHandler`, `IQuery`, `IQueryHandler`), `Behaviors/ValidationBehavior`, `<Feature>/Commands/<CasoDeUso>/` e `<Feature>/Queries/<CasoDeUso>/` (comando ou consulta + handler + validador), `<Feature>/Services/` (serviços de aplicação que concentram as regras de negócio e a orquestração), `Contracts/<Feature>/` (DTOs de resposta), `Interfaces/`, `Configuration/DependencyInjectionConfig` |
| **3 - Domain** | `src/Domain/*.Domain` | `Entities/Abstracts/Entity`, `Models/` (agregados e entidades), `Enums/`, `Errors/`, `Exceptions/`, `Helpers/` (`Result`, `Error`), `Interfaces/Repository/`, `Interfaces/Service/` (`IXxxService`) e `Interfaces/Common/IUnitOfWork` |
| **4 - Infra** | `src/Infrastructure/*.Infrastructure` | `Context/` (DbContext), `EntityConfiguration/` (`XxxMap`), `Repository/`, `UnitOfWork/`, `Migrations/`, `Health/`, `Configurations/DependencyInjectionConfig` (`AddInfrastructure`), mais `Messaging/`, `Outbox/` e `Security/` |
| **5 - Tests** | `src/Tests/*` | Unitários, integração, contrato, gateway e `CashFlow.LoadTest` |

Dependências apontam para dentro: `Api → Application → Domain` e `Infrastructure → Application/Domain`. A Api referencia a Infrastructure apenas para compor a injeção de dependência.

Fluxo de uma escrita: `Controller → ICommandHandler (ValidationBehavior → XxxCommandHandler) → IXxxService → IUnitOfWork (repositórios) + CommitAsync()`. Leituras: `Controller → IQueryHandler → IXxxService → IUnitOfWork (repositórios)`. O handler apenas traduz comando/consulta em chamada ao serviço e o resultado em DTO de resposta; regras de negócio, idempotência, retentativas por concorrência e publicação no outbox ficam no serviço, e as invariantes de cada agregado ficam no próprio agregado (`Account.Register`).

No Consolidado, os projetores de mensagens (`TransactionRegisteredProjector`) usam o `IUnitOfWork` diretamente, pois a unidade de trabalho é uma mensagem do broker e não um caso de uso HTTP.

### Convenções
- Camadas, nomes de pastas e agrupamento da solução (1 a 5).
- Contratos `ICommand`/`ICommandHandler`/`IQuery`/`IQueryHandler` e o `ValidationBehavior` com FluentValidation aplicado somente a comandos.
- Comandos e consultas como `sealed record`, com sufixos `Command`, `Query`, `CommandHandler`, `QueryHandler` e `CommandValidator`.
- Interfaces de repositório e `IUnitOfWork` no Domain; implementações em `Repository/` e `UnitOfWork/` na Infrastructure; mapeamentos EF como `XxxMap : IEntityTypeConfiguration<T>`.
- Controllers com `[Authorize]`, um por recurso, e DI por métodos de extensão (`AddApiConfig`, `AddApplication`, `AddInfrastructure`).

### Escolhas de implementação, e por quê
| Em vez de | Aqui | Motivo |
|---|---|---|
| MediatR 12 | Handlers resolvidos direto pela DI, com o `ValidationBehavior` como decorator | MediatR 13+ tem licença comercial ([ADR-0007](0007-bibliotecas-e-licencas.md)); o contrato dos handlers é o mesmo |
| `ResultWrapper<T>` e exceções tipadas convertidas em HTTP por middleware | `Result<T>`/`Error` mapeados para ProblemDetails (RFC 9457) no `ApiControllerBase` | Um único formato de resposta; exceções só para o que é excepcional (concorrência otimista, unicidade) |
| Repositórios injetados individualmente em cada handler | `IUnitOfWork` expõe os repositórios (`AccountRepository`, `TransactionRepository`) e também `CommitAsync` e `Reset` | Os repositórios compartilham o mesmo `DbContext`; ao passar pela unidade de trabalho, leitura, escrita e commit ficam na mesma transação e o serviço recebe uma única dependência |
| Regras de negócio e orquestração no handler | Camada de serviço (`IXxxService` no Domain, implementação em `Application/<Feature>/Services`) concentra as regras de negócio; o handler só delega | O fluxo de registro de lançamento (idempotência por chave, retentativa em conflito de concorrência, outbox) é reutilizável e testável sem o pipeline de comandos; handlers de comando e de consulta compartilham as mesmas regras de acesso (por exemplo, conta inexistente) |
| `Entity` com auditoria e soft delete | `Entity` com `Id`; lançamentos são imutáveis e nunca apagados | Em livro-razão a correção é um novo lançamento, não uma exclusão lógica |
| Mapster | Mapeamento explícito (`XxxResponse.From`) | Poucos DTOs; evita reflexão e mantém o mapeamento visível |

## Alternativas consideradas
- **MediatR e `ResultWrapper`:** trariam a dependência de licença comercial e dois padrões de erro.
- **Sem camada de serviço (regras no handler, repositórios injetados):** descartada, porque o handler passaria a misturar tradução de contrato com orquestração, e consultas e comandos duplicariam regras comuns.
- **Projeto único por serviço (só pastas):** menos projetos, mas o compilador não impede violações de camada.
- **Minimal APIs:** usadas no Identity e no Gateway, que são serviços de suporte pequenos ([ADR-0008](0008-servico-de-identidade-proprio.md)). Nos serviços de negócio ficamos com controllers.

## Consequências
- ➕ Comandos, consultas, repositórios e mapeamentos ficam sempre nos mesmos lugares, previsíveis para quem chega ao código.
- ➕ Regras de domínio cobertas por testes unitários rápidos; trocar RabbitMQ ou PostgreSQL afeta só a Infrastructure.
- ➖ Mais projetos e algum mapeamento explícito entre camadas.
