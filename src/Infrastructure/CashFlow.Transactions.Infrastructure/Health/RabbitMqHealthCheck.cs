using CashFlow.Transactions.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CashFlow.Transactions.Infrastructure.Health;

internal sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnection _connection;

    public RabbitMqHealthCheck(RabbitMqConnection connection)
    {
        _connection = connection;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var conn = await _connection.GetAsync(cancellationToken);
            return conn.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded("Conexão RabbitMQ fechada.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("RabbitMQ indisponível; outbox acumulando.", ex);
        }
    }
}
