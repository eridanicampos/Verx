namespace CashFlow.Consolidation.Domain.Models;

public sealed class ConsolidatedAccount
{
    private ConsolidatedAccount()
    {
    }

    public Guid AccountId { get; private set; }

    public DateTimeOffset OpenedAt { get; private set; }

    public DateTimeOffset? LastEntryAt { get; private set; }

    public static ConsolidatedAccount Restore(Guid accountId, DateTimeOffset openedAt, DateTimeOffset? lastEntryAt) => new()
    {
        AccountId = accountId,
        OpenedAt = openedAt,
        LastEntryAt = lastEntryAt,
    };
}
