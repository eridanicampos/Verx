using CashFlow.Consolidation.Domain.Models;

namespace CashFlow.Consolidation.Domain.Interfaces.Repository;

public interface ILedgerEntryRepository
{
    Task<bool> TryAddAsync(LedgerEntry entry, CancellationToken cancellationToken);
}
