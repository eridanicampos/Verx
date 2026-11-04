using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CashFlow.Transactions.Infrastructure.Health;

internal sealed class OutboxBacklogHealthCheck : IHealthCheck
{
    private readonly TransactionsDbContext _db;
    private readonly IOptions<OutboxOptions> _options;
    private readonly TimeProvider _clock;

    public OutboxBacklogHealthCheck(TransactionsDbContext db, IOptions<OutboxOptions> options, TimeProvider clock)
    {
        _db = db;
        _options = options;
        _clock = clock;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var threshold = _clock.GetUtcNow().AddMinutes(-2);
        var pendingOld = await _db.OutboxMessages.CountAsync(m => m.ProcessedAt == null && m.CreatedAt < threshold, cancellationToken);
        var parked = await _db.OutboxMessages.CountAsync(m => m.ProcessedAt == null && m.Attempts >= _options.Value.MaxAttempts, cancellationToken);

        var data = new Dictionary<string, object> { ["pendingOlderThan2Min"] = pendingOld, ["parked"] = parked };
        return pendingOld == 0 && parked == 0
            ? HealthCheckResult.Healthy(data: data)
            : HealthCheckResult.Degraded("Backlog no outbox.", data: data);
    }
}
