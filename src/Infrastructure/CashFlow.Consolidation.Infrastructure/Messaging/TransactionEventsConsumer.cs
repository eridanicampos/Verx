using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using CashFlow.Consolidation.Application.Projections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CashFlow.Consolidation.Infrastructure.Messaging;

public sealed partial class TransactionEventsConsumer : BackgroundService
{
    public static readonly ActivitySource ActivitySource = new("CashFlow.Consolidation.Consumer");
    private static readonly Meter Meter = new("CashFlow.Consolidation.Consumer");
    private static readonly Counter<long> Processed = Meter.CreateCounter<long>("cashflow.consumer.processed");
    private static readonly Counter<long> Duplicates = Meter.CreateCounter<long>("cashflow.consumer.duplicates");
    private static readonly Counter<long> Retried = Meter.CreateCounter<long>("cashflow.consumer.retried");
    private static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("cashflow.consumer.deadlettered");

    private readonly RabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RabbitMqOptions> _options;
    private readonly ILogger<TransactionEventsConsumer> _logger;

    private IChannel? _channel;

    public TransactionEventsConsumer(
        RabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<TransactionEventsConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    public bool IsConsuming => _channel is { IsOpen: true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var o = _options.Value;
        if (!o.ConsumerEnabled)
        {
            return;
        }

        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumingAsync(o, stoppingToken);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogConnectFailed(_logger, ex, delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    private async Task StartConsumingAsync(RabbitMqOptions o, CancellationToken cancellationToken)
    {
        var conn = await _connection.GetAsync(cancellationToken);
        var channel = await conn.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await RabbitMqTopology.DeclareAsync(channel, o, cancellationToken);
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: o.PrefetchCount, global: false, cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, o, cancellationToken);
        await channel.BasicConsumeAsync(o.Queue, autoAck: false, consumer, cancellationToken);
        _channel = channel;
        LogStarted(_logger, o.Queue);
    }

    internal async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, RabbitMqOptions o, CancellationToken cancellationToken)
    {
        var props = delivery.BasicProperties;
        var parent = props.Headers is not null && props.Headers.TryGetValue("traceparent", out var tp) && tp is byte[] tpBytes
            ? Encoding.UTF8.GetString(tpBytes)
            : null;
        using var activity = ActivitySource.StartActivity("consume " + props.Type, ActivityKind.Consumer, parent);
        activity?.SetTag("messaging.message.id", props.MessageId);

        try
        {
            if (!Guid.TryParse(props.MessageId, out var messageId))
            {
                throw new Application.Messages.PermanentMessageException("MessageId ausente ou inválido.");
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
            var outcome = await dispatcher.DispatchAsync(messageId, props.Type, delivery.Body, cancellationToken);

            (outcome == Application.Messages.MessageOutcome.Duplicate ? Duplicates : Processed).Add(1);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var retryCount = FailurePolicy.ReadRetryCount(props.Headers);
            var action = FailurePolicy.Decide(ex, retryCount, o.MaxRetries);

            if (action == FailureAction.Retry)
            {
                await RepublishForRetryAsync(channel, delivery, o, retryCount + 1, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                Retried.Add(1);
                LogRetry(_logger, ex, props.MessageId, retryCount + 1, o.MaxRetries);
            }
            else
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                DeadLettered.Add(1);
                LogDeadLettered(_logger, ex, props.MessageId, retryCount);
            }
        }
    }

    private static async Task RepublishForRetryAsync(IChannel channel, BasicDeliverEventArgs delivery, RabbitMqOptions o, int retryCount, CancellationToken cancellationToken)
    {
        var source = delivery.BasicProperties;
        var headers = source.Headers is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(source.Headers, StringComparer.Ordinal);
        headers[FailurePolicy.RetryCountHeader] = retryCount;

        var properties = new BasicProperties
        {
            MessageId = source.MessageId,
            Type = source.Type,
            ContentType = source.ContentType,
            ContentEncoding = source.ContentEncoding,
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = source.Timestamp,
            AppId = source.AppId,
            Headers = headers,
        };

        await channel.BasicPublishAsync(string.Empty, o.RetryQueue, mandatory: true, properties, delivery.Body, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        var channel = Interlocked.Exchange(ref _channel, null);
        if (channel is null)
        {
            return;
        }

        try
        {
            await channel.CloseAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or RabbitMQ.Client.Exceptions.AlreadyClosedException or OperationCanceledException)
        {
        }
        finally
        {
            await channel.DisposeAsync();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumindo fila {Queue}")]
    private static partial void LogStarted(ILogger logger, string queue);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Broker indisponível; nova tentativa em {Seconds}s")]
    private static partial void LogConnectFailed(ILogger logger, Exception ex, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha transitória na mensagem {MessageId}; retry {Retry}/{Max}")]
    private static partial void LogRetry(ILogger logger, Exception ex, string? messageId, int retry, int max);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensagem {MessageId} enviada para DLQ após {Retries} retries")]
    private static partial void LogDeadLettered(ILogger logger, Exception ex, string? messageId, int retries);
}
