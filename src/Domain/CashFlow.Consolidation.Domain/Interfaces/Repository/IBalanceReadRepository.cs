using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;

namespace CashFlow.Consolidation.Domain.Interfaces.Repository;

public sealed record BalanceAggregate(decimal Credits, decimal Debits);

public interface IBalanceReadRepository
{
    Task<ConsolidatedAccount?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken);

    Task<BalanceAggregate> SumDailyBeforeAsync(Guid accountId, DateOnly date, CancellationToken cancellationToken);

    Task<BalanceAggregate> SumEntriesOfDayUntilAsync(Guid accountId, DateOnly date, DateTimeOffset until, CancellationToken cancellationToken);

    Task<IReadOnlyList<DailyBalance>> ListDailyAsync(Guid accountId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
