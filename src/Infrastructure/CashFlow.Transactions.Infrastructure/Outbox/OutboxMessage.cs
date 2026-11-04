namespace CashFlow.Transactions.Infrastructure.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid id, string type, string payload, DateTimeOffset createdAt, string? traceParent)
    {
        Id = id;
        Type = type;
        Payload = payload;
        CreatedAt = createdAt;
        NextAttemptAt = createdAt;
        TraceParent = traceParent;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public string? TraceParent { get; private set; }

    public void MarkPublished(DateTimeOffset now)
    {
        ProcessedAt = now;
        Attempts++;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nextAttemptAt)
    {
        Attempts++;
        LastError = error.Length > 2000 ? error[..2000] : error;
        NextAttemptAt = nextAttemptAt;
    }
}
