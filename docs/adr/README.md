# Registro de Decisões Arquiteturais (ADR)

Formato baseado em Michael Nygard: contexto → decisão → alternativas → consequências. Uma decisão nunca é apagada: quando mudar, cria-se um novo ADR que a substitui (`Status: Substituído por ADR-XXXX`).

Cada ADR começa com um front matter YAML (`status`, `date`) no formato [MADR](https://adr.github.io/madr/), lido pelo Structurizr (`!adrs ../adr madr` em [`c4/workspace.dsl`](../c4/workspace.dsl)). Ao mudar o status no texto, atualize também o front matter (`accepted`, `superseded`, `deprecated`, `rejected`, `proposed`).

| # | Decisão | Status |
|---|---|---|
| [0001](0001-registro-de-decisoes.md) | Registrar decisões arquiteturais em ADRs versionados | Aceito |
| [0002](0002-microsservicos-lancamentos-e-consolidado.md) | Dois microsserviços de negócio (Lançamentos × Consolidado) com database-per-service | Aceito |
| [0003](0003-mensageria-rabbitmq.md) | RabbitMQ como broker de eventos (quorum queues, retry e DLQ) | Aceito |
| [0004](0004-contratos-sem-projeto-compartilhado.md) | Contratos de eventos sem projeto compartilhado (sem `CashFlow.Shared`) | Aceito |
| [0005](0005-autenticacao-jwt-rs256-e-refresh-token.md) | JWT RS256 de curta duração + refresh token opaco rotativo | Aceito |
| [0006](0006-consistencia-eventual-e-projecao-comutativa.md) | Consistência eventual no consolidado com projeção comutativa | Aceito |
| [0007](0007-bibliotecas-e-licencas.md) | Somente bibliotecas com licença permissiva; versões fixadas | Aceito |
| [0008](0008-servico-de-identidade-proprio.md) | Serviço de identidade próprio e substituível por IdP gerenciado | Aceito |
| [0009](0009-azure-container-apps.md) | Azure Container Apps como plataforma de execução | Aceito |
| [0010](0010-runtime-dotnet-10-lts.md) | Runtime .NET 10 LTS (em vez de .NET 8) | Aceito |
| [0011](0011-postgresql.md) | PostgreSQL como banco relacional dos serviços | Aceito |
| [0012](0012-transactional-outbox-e-inbox.md) | Transactional Outbox no produtor e Inbox no consumidor | Aceito |
| [0013](0013-clean-architecture.md) | Clean Architecture com CQRS, Repository e Unit of Work | Aceito |
| [0014](0014-api-gateway-e-protecao-de-borda.md) | API Gateway (YARP) + Front Door/WAF como proteção de borda | Aceito |
| [0015](0015-teste-de-carga-proprio.md) | Teste de carga próprio em modelo aberto (CashFlow.LoadTest) | Aceito |
