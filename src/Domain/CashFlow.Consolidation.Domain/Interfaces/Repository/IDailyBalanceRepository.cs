using CashFlow.Consolidation.Domain.Models;

namespace CashFlow.Consolidation.Domain.Interfaces.Repository;

public interface IDailyBalanceRepository
{
    Task ApplyAsync(LedgerEntry entry, DateTimeOffset now, CancellationToken cancellationToken);
}
