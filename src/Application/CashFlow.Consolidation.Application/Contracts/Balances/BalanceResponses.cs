namespace CashFlow.Consolidation.Application.Contracts.Balances;

public sealed record BalanceResponse(
    Guid AccountId,
    DateTimeOffset At,
    decimal Balance,
    decimal TotalCredits,
    decimal TotalDebits,
    DateTimeOffset? LastEntryAt,
    string Consistency = "eventual");

public sealed record DailyBalanceResponse(
    DateOnly Date,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal Net,
    decimal ClosingBalance,
    int EntryCount);
