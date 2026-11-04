using CashFlow.Transactions.Application.IntegrationEvents;

namespace CashFlow.Transactions.Application.Interfaces;

public interface IOutbox
{
    void Enqueue<TEvent>(TEvent integrationEvent)
        where TEvent : IIntegrationEvent;
}
