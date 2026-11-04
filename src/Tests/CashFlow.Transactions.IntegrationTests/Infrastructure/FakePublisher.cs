using System.Collections.Concurrent;
using CashFlow.Transactions.Infrastructure.Messaging;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

public sealed class FakePublisher : IMessagePublisher
{
    public ConcurrentQueue<OutgoingMessage> Published { get; } = new();

    public bool Fail { get; set; }

    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new IOException("Broker indisponível (simulado).");
        }

        Published.Enqueue(message);
        return Task.CompletedTask;
    }
}
