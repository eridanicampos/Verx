namespace CashFlow.Consolidation.Domain.Interfaces.Repository;

public interface IConsolidatedAccountRepository
{
    Task UpsertAsync(Guid accountId, DateTimeOffset openedAt, DateTimeOffset? lastEntryAt, CancellationToken cancellationToken);
}
