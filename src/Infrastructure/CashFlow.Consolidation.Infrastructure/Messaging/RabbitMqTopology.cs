using RabbitMQ.Client;

namespace CashFlow.Consolidation.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public static async Task DeclareAsync(IChannel channel, RabbitMqOptions o, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(o.SourceExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(o.DeadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            o.DeadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(o.DeadLetterQueue, o.DeadLetterExchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            o.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = o.DeadLetterExchange,
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(o.Queue, o.SourceExchange, routingKey: "transaction.registered.v1", cancellationToken: cancellationToken);
        await channel.QueueBindAsync(o.Queue, o.SourceExchange, routingKey: "account.opened.v1", cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            o.RetryQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-message-ttl"] = (int)o.RetryDelay.TotalMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = o.Queue,
            },
            cancellationToken: cancellationToken);
    }
}
