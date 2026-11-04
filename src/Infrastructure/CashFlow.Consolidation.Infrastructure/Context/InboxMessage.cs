namespace CashFlow.Consolidation.Infrastructure.Context;

public sealed class InboxMessage
{
    public Guid MessageId { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; set; }
}
