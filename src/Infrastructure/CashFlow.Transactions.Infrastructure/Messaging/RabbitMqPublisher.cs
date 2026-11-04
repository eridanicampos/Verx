using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public RabbitMqPublisher(RabbitMqConnection connection, IOptions<RabbitMqOptions> options)
    {
        _connection = connection;
        _options = options;
    }

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        var o = _options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(o.PublishTimeout);

        await _lock.WaitAsync(timeout.Token);
        try
        {
            var channel = await GetChannelAsync(o, timeout.Token);
            var headers = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["x-event-type"] = message.Type,
            };
            if (message.TraceParent is not null)
            {
                headers["traceparent"] = message.TraceParent;
            }

            var properties = new BasicProperties
            {
                MessageId = message.MessageId.ToString(),
                Type = message.Type,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(message.CreatedAt.ToUnixTimeSeconds()),
                AppId = "cashflow-transactions",
                Headers = headers,
            };

            await channel.BasicPublishAsync(
                exchange: o.Exchange,
                routingKey: message.Type,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: timeout.Token);
        }
        catch
        {
            await ResetChannelAsync();
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(RabbitMqOptions o, CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        var conn = await _connection.GetAsync(cancellationToken);
        var channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(o.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        _channel = channel;
        return channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is not null)
        {
            try
            {
                await _channel.DisposeAsync();
            }
            catch (Exception)
            {
            }

            _channel = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ResetChannelAsync();
        _lock.Dispose();
    }
}
