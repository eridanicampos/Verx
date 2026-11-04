using CashFlow.Consolidation.Domain.Helpers;
using CashFlow.Consolidation.Domain.Models;

namespace CashFlow.Consolidation.Domain.Interfaces.Service;

public interface IBalanceService
{
    Task<Result<AccountBalance>> GetBalanceAtAsync(Guid accountId, DateTimeOffset? at, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DailyBalanceStatement>>> GetDailyBalancesAsync(
        Guid accountId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}
