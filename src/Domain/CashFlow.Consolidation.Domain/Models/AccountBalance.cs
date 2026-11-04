namespace CashFlow.Consolidation.Domain.Models;

public sealed record AccountBalance(
    Guid AccountId,
    DateTimeOffset At,
    decimal TotalCredits,
    decimal TotalDebits,
    DateTimeOffset? LastEntryAt)
{
    public decimal Balance => TotalCredits - TotalDebits;
}
