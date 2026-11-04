# C4 — Nível 3: Componentes (Lançamentos e Consolidado)

Os dois serviços de negócio seguem a Clean Architecture com CQRS, Repository e Unit of Work (camadas 1 - Presentation, 2 - Application, 3 - Domain, 4 - Infra). As dependências apontam para dentro: o Domain define entidades e as interfaces de repositório e de Unit of Work, a Application orquestra os casos de uso e a Infrastructure implementa as interfaces ([ADR-0013](../adr/0013-clean-architecture.md)).

## Serviço de Lançamentos

```mermaid
flowchart LR
    subgraph Api["CashFlow.Transactions.Api (Apresentação)"]
        EP[Controllers REST<br/>AccountsController · TransactionsController]
        AUTH[JWT Bearer + políticas por escopo]
        RL[Rate limiter por cliente]
        HC[Health checks live/ready]
    end
    subgraph App["CashFlow.Transactions.Application (Casos de uso)"]
        VB[ValidationBehavior<br/>FluentValidation]
        RT[RegisterTransactionCommandHandler]
        OA[OpenAccountCommandHandler]
        Q[Queries: GetAccountById / ListTransactions]
        SVC[AccountService · TransactionService<br/>regras de negócio: idempotência, retry de concorrência, PII]
        PORTS[[Interfaces: IOutbox, IPiiProtector]]
        IE[Contratos de integração V1]
    end
    subgraph Dom["CashFlow.Transactions.Domain"]
        ACC[Account — agregado<br/>invariante de saldo]
        TX[Transaction — imutável]
        REPO[[IAccountRepository · ITransactionRepository · IUnitOfWork]]
        ISVC[[IAccountService · ITransactionService]]
    end
    subgraph Infra["CashFlow.Transactions.Infrastructure"]
        EF[(TransactionsDbContext<br/>EntityConfiguration · Repository · UnitOfWork)]
        OB[EfOutbox]
        OP[OutboxProcessor<br/>FOR UPDATE SKIP LOCKED]
        PUB[RabbitMqPublisher<br/>publisher confirms]
        PII[AesGcmPiiProtector]
    end
    EP --> VB --> RT & OA
    EP --> Q
    RT & OA & Q --> SVC
    SVC -.implementa.- ISVC
    SVC --> ACC --> TX
    SVC --> REPO & PORTS
    REPO -.implementa.- EF
    PORTS -.implementa.- OB & PII
    OP --> PUB --> MQ{{RabbitMQ}}
    OB --> EF
```

## Serviço de Consolidado

```mermaid
flowchart LR
    MQ{{RabbitMQ}} --> CON
    subgraph Infra["CashFlow.Consolidation.Infrastructure"]
        CON[TransactionEventsConsumer<br/>ack manual, prefetch]
        FP[FailurePolicy<br/>retry × DLQ]
        UOW[(UnitOfWork<br/>transação + execution strategy)]
        PS[(Inbox · LedgerEntry · DailyBalance · ConsolidatedAccount<br/>repositórios INSERT … ON CONFLICT)]
        RR[(BalanceReadRepository)]
    end
    subgraph App["CashFlow.Consolidation.Application"]
        DISP[MessageDispatcher<br/>tolerant reader]
        PROJ[TransactionRegisteredProjector<br/>Inbox + chave natural]
        BAL[GetBalanceAtQueryHandler]
        DAY[GetDailyBalancesQueryHandler]
        BSVC[BalanceService<br/>saldo no instante · relatório diário]
    end
    subgraph Dom["CashFlow.Consolidation.Domain"]
        LE[LedgerEntry]
        DB[DailyBalance]
        CAL[BusinessCalendar UTC]
    end
    subgraph Api["CashFlow.Consolidation.Api"]
        EP[BalancesController<br/>GET /balance?at= · GET /daily-balances]
    end
    CON --> DISP --> PROJ --> UOW --> PS
    CON --> FP
    EP --> BAL & DAY --> BSVC --> UOW --> RR
    PROJ --> LE
    BSVC --> CAL
```
