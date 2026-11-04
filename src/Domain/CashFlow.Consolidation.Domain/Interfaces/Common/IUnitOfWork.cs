using CashFlow.Consolidation.Domain.Interfaces.Repository;

namespace CashFlow.Consolidation.Domain.Interfaces.Common;

public interface IUnitOfWork
{
    IBalanceReadRepository BalanceReadRepository { get; }

    IInboxRepository InboxRepository { get; }

    ILedgerEntryRepository LedgerEntryRepository { get; }

    IDailyBalanceRepository DailyBalanceRepository { get; }

    IConsolidatedAccountRepository ConsolidatedAccountRepository { get; }

    // As projeções gravam via upsert (ON CONFLICT) direto no banco; a unidade de trabalho é a
    // transação: tudo o que os repositórios executam dentro de "work" é confirmado ou desfeito junto.
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
