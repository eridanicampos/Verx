using System.Diagnostics;
using System.Diagnostics.Metrics;
using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Infrastructure.Outbox;

public sealed partial class OutboxProcessor : Microsoft.Extensions.Hosting.BackgroundService
{
    public static readonly ActivitySource ActivitySource = new("CashFlow.Transactions.Outbox");
    private static readonly Meter Meter = new("CashFlow.Transactions.Outbox");
    private static readonly Counter<long> Published = Meter.CreateCounter<long>("cashflow.outbox.published");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("cashflow.outbox.failed");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMessagePublisher _publisher;
    private readonly IOptions<OutboxOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IMessagePublisher publisher,
        IOptions<OutboxOptions> options,
        TimeProvider clock,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = _options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            int processed;
            try
            {
                processed = await ProcessBatchAsync(stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogBatchFailed(_logger, ex);
                processed = 0;
            }

            if (processed < settings.BatchSize)
            {
                await Task.Delay(settings.PollingInterval, _clock, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            (Db: db, Processor: this),
            static async (_, state, ct) => await state.Processor.ProcessLockedBatchAsync(state.Db, ct),
            verifySucceeded: null,
            cancellationToken);
    }

    private async Task<int> ProcessLockedBatchAsync(TransactionsDbContext db, CancellationToken cancellationToken)
    {
        var settings = _options.Value;
        db.ChangeTracker.Clear();
        await using var dbTransaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var now = _clock.GetUtcNow();
        var batch = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM transactions.outbox_messages
                WHERE processed_at IS NULL AND attempts < {settings.MaxAttempts} AND next_attempt_at <= {now}
                ORDER BY created_at
                LIMIT {settings.BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            using var activity = ActivitySource.StartActivity("outbox.publish", ActivityKind.Producer, message.TraceParent);
            activity?.SetTag("messaging.message.id", message.Id);
            activity?.SetTag("messaging.message.type", message.Type);
            try
            {
                await _publisher.PublishAsync(
                    new OutgoingMessage(message.Id, message.Type, message.Payload, message.CreatedAt, message.TraceParent),
                    cancellationToken);
                message.MarkPublished(_clock.GetUtcNow());
                Published.Add(1);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var delay = Backoff(message.Attempts + 1, settings.MaxBackoff);
                message.MarkFailed(ex.Message, _clock.GetUtcNow() + delay);
                Failed.Add(1);
                LogPublishFailed(_logger, ex, message.Id, message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);
        return batch.Count;
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        if (now - _lastCleanup < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastCleanup = now;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        var threshold = now - _options.Value.Retention;
        await db.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < threshold)
            .ExecuteDeleteAsync(cancellationToken);
    }

    internal static TimeSpan Backoff(int attempt, TimeSpan max)
    {
        var seconds = Math.Min(Math.Pow(2, attempt), max.TotalSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar lote do outbox")]
    private static partial void LogBatchFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao publicar mensagem {MessageId} (tentativa {Attempt})")]
    private static partial void LogPublishFailed(ILogger logger, Exception ex, Guid messageId, int attempt);
}
