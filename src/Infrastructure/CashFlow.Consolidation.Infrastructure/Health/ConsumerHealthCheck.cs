using CashFlow.Consolidation.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CashFlow.Consolidation.Infrastructure.Health;

internal sealed class ConsumerHealthCheck : IHealthCheck
{
    private readonly TransactionEventsConsumer _consumer;

    public ConsumerHealthCheck(TransactionEventsConsumer consumer)
    {
        _consumer = consumer;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_consumer.IsConsuming
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded("Consumidor de eventos não está ativo; consolidado pode estar defasado."));
}
