namespace CashFlow.Transactions.Application.IntegrationEvents;

public sealed record TransactionRegisteredV1(
    Guid EventId,
    Guid TransactionId,
    Guid AccountId,
    string Type,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventType => "transaction.registered.v1";
}
