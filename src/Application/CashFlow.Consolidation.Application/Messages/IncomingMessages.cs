namespace CashFlow.Consolidation.Application.Messages;

public sealed record TransactionRegisteredMessage(
    Guid EventId,
    Guid TransactionId,
    Guid AccountId,
    string Type,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt)
{
    public const string MessageType = "transaction.registered.v1";
}

public sealed record AccountOpenedMessage(Guid EventId, Guid AccountId, DateTimeOffset OccurredAt)
{
    public const string MessageType = "account.opened.v1";
}
