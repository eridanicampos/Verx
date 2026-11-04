namespace CashFlow.Transactions.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Enabled { get; set; } = true;

    public int BatchSize { get; set; } = 100;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);

    public int MaxAttempts { get; set; } = 20;

    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
