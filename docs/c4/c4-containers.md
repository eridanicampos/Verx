# C4 — Nível 2: Containers

Inclui a camada de UI/Cliente (Console Web), a borda (Front Door + WAF), o Gateway, os três serviços e seus armazenamentos. Fonte formal: [`workspace.dsl`](workspace.dsl).

```mermaid
C4Container
    title CashFlow — Diagrama de Containers (C2)

    Person(operador, "Operador / Analista")
    System_Ext(canais, "Canais Digitais", "Clientes de API")

    Container_Boundary(cf, "CashFlow") {
        Container(web, "Console Web", "Nginx + JavaScript", "SPA; access token em memória; refresh em cookie HttpOnly")
        Container(edge, "Front Door + WAF", "Azure Front Door Premium", "TLS, OWASP DRS, Bot Manager, rate limit por IP")
        Container(gateway, "API Gateway", "ASP.NET Core 10 + YARP", "Roteamento, rate limiting, CORS, limite de payload")
        Container(identity, "Identidade", "ASP.NET Core 10", "JWT RS256, refresh token rotativo, JWKS, lockout")
        Container(transactions, "Lançamentos", "ASP.NET Core 10", "Contas, lançamentos, invariante de saldo, Outbox")
        Container(consolidation, "Consolidado", "ASP.NET Core 10", "Inbox idempotente, projeção diária, saldo em T")
        ContainerQueue(broker, "Broker", "RabbitMQ 4.1", "Exchange topic, filas quorum, retry TTL, DLQ")
        ContainerDb(idDb, "identity db", "PostgreSQL", "Usuários, refresh tokens (hash)")
        ContainerDb(txDb, "transactions db", "PostgreSQL", "Contas (PII cifrada), lançamentos, outbox")
        ContainerDb(coDb, "consolidation db", "PostgreSQL", "Inbox, ledger projetado, consolidados diários")
    }

    Rel(operador, web, "Usa", "HTTPS")
    Rel(canais, edge, "Chama API", "HTTPS")
    Rel(web, edge, "API same-origin", "HTTPS")
    Rel(edge, gateway, "Tráfego inspecionado", "HTTPS")
    Rel(gateway, identity, "/auth")
    Rel(gateway, transactions, "/api/v1/accounts/**")
    Rel(gateway, consolidation, "/balance, /daily-balances")
    Rel(transactions, txDb, "Lançamento + outbox na mesma transação", "TLS")
    Rel(transactions, broker, "Publica eventos", "AMQP + confirms")
    Rel(broker, consolidation, "Entrega eventos", "AMQP, ack manual")
    Rel(consolidation, coDb, "Inbox + projeção na mesma transação", "TLS")
    Rel(identity, idDb, "Lê/escreve", "TLS")
    Rel(transactions, identity, "JWKS")
    Rel(consolidation, identity, "JWKS")

    UpdateLayoutConfig($c4ShapeInRow="4", $c4BoundaryInRow="1")
```

## Responsabilidades e decisões por container

| Container | Tecnologia | Escala | Dados que possui | Decisões |
|---|---|---|---|---|
| Console Web | Nginx (imagem não-root) + JS sem framework | Horizontal, estático | Nenhum | CSP restritiva; token em memória; Idempotency-Key gerada no cliente |
| Front Door + WAF | Azure Front Door Premium | Gerenciado | — | [ADR-0014](../adr/0014-api-gateway-e-protecao-de-borda.md) |
| API Gateway | YARP | Horizontal, stateless | — | [ADR-0014](../adr/0014-api-gateway-e-protecao-de-borda.md) |
| Identidade | ASP.NET Core | Horizontal | Usuários, refresh tokens | [ADR-0005](../adr/0005-autenticacao-jwt-rs256-e-refresh-token.md), [ADR-0008](../adr/0008-servico-de-identidade-proprio.md) |
| Lançamentos | ASP.NET Core, Clean Architecture | Horizontal (concorrência otimista + SKIP LOCKED no outbox) | Contas, lançamentos, outbox | [ADR-0002](../adr/0002-microsservicos-lancamentos-e-consolidado.md), [ADR-0012](../adr/0012-transactional-outbox-e-inbox.md) |
| Broker | RabbitMQ (quorum queues) | Cluster de 3 nós (alvo prod) | Mensagens em trânsito | [ADR-0003](../adr/0003-mensageria-rabbitmq.md) |
| Consolidado | ASP.NET Core, Clean Architecture | Horizontal + KEDA por profundidade de fila | Inbox, ledger projetado, diários | [ADR-0006](../adr/0006-consistencia-eventual-e-projecao-comutativa.md), [ADR-0012](../adr/0012-transactional-outbox-e-inbox.md) |
| PostgreSQL | Flexible Server (HA zona redundante) | Vertical + réplicas de leitura | Database-per-service | [ADR-0011](../adr/0011-postgresql.md) |
