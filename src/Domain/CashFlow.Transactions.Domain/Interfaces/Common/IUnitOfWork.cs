using CashFlow.Transactions.Domain.Interfaces.Repository;

namespace CashFlow.Transactions.Domain.Interfaces.Common;

public interface IUnitOfWork
{
    IAccountRepository AccountRepository { get; }

    ITransactionRepository TransactionRepository { get; }

    Task CommitAsync(CancellationToken cancellationToken);

    void Reset();
}
