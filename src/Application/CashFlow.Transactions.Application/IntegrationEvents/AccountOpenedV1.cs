namespace CashFlow.Transactions.Application.IntegrationEvents;

public sealed record AccountOpenedV1(
    Guid EventId,
    Guid AccountId,
    DateTimeOffset OccurredAt) : IIntegrationEvent
{
    public static string EventType => "account.opened.v1";
}
