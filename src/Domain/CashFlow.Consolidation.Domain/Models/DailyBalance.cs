namespace CashFlow.Consolidation.Domain.Models;

public sealed class DailyBalance
{
    private DailyBalance()
    {
    }

    public Guid AccountId { get; private set; }

    public DateOnly Date { get; private set; }

    public decimal TotalCredits { get; private set; }

    public decimal TotalDebits { get; private set; }

    public int EntryCount { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public decimal Net => TotalCredits - TotalDebits;

    public static DailyBalance Restore(Guid accountId, DateOnly date, decimal credits, decimal debits, int count, DateTimeOffset updatedAt) => new()
    {
        AccountId = accountId,
        Date = date,
        TotalCredits = credits,
        TotalDebits = debits,
        EntryCount = count,
        UpdatedAt = updatedAt,
    };
}
