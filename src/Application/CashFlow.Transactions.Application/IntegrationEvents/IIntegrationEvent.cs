namespace CashFlow.Transactions.Application.IntegrationEvents;

public interface IIntegrationEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }

    static abstract string EventType { get; }
}
