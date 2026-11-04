using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Domain.Interfaces.Service;

public interface ITransactionService
{
    Task<Result<TransactionRegistration>> RegisterAsync(
        Guid accountId,
        TransactionType type,
        decimal amount,
        string? description,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<Transaction>>> ListByAccountAsync(
        Guid accountId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken);
}
