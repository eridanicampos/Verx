namespace CashFlow.Consolidation.IntegrationTests.Infrastructure;

public sealed record BalanceDto(Guid AccountId, DateTimeOffset At, decimal Balance, decimal TotalCredits, decimal TotalDebits, DateTimeOffset? LastEntryAt);

public sealed record DailyDto(DateOnly Date, decimal TotalCredits, decimal TotalDebits, decimal Net, decimal ClosingBalance, int EntryCount);

public static class Events
{
    public static object Transaction(Guid accountId, string type, decimal amount, DateTimeOffset? occurredAt = null, Guid? transactionId = null) => new
    {
        eventId = Guid.NewGuid(),
        transactionId = transactionId ?? Guid.NewGuid(),
        accountId,
        type,
        amount,
        currency = "BRL",
        occurredAt = occurredAt ?? DateTimeOffset.UtcNow,
    };

    public static object AccountOpened(Guid accountId, DateTimeOffset? at = null) => new
    {
        eventId = Guid.NewGuid(),
        accountId,
        occurredAt = at ?? DateTimeOffset.UtcNow.AddMinutes(-1),
    };
}
