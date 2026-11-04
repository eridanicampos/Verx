namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed record OutgoingMessage(Guid MessageId, string Type, string Payload, DateTimeOffset CreatedAt, string? TraceParent);

public interface IMessagePublisher
{
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}
