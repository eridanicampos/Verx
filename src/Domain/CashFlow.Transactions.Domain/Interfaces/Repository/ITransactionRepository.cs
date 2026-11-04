using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Domain.Interfaces.Repository;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdempotencyKeyAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken);

    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Transaction>> ListByAccountAsync(
        Guid accountId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken);

    void Add(Transaction transaction);
}
