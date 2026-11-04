using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

[assembly: AssemblyFixture(typeof(CashFlow.Consolidation.IntegrationTests.Infrastructure.TestInfrastructure))]

namespace CashFlow.Consolidation.IntegrationTests.Infrastructure;

public sealed class TestInfrastructure : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RabbitMqContainer? _rabbit;

    public string PostgresAdminConnectionString { get; private set; } = string.Empty;

    public Uri RabbitMqUri { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var pg = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(pg))
        {
            _postgres = new PostgreSqlBuilder("postgres:17-alpine").WithCommand("-c", "max_connections=500").Build();
            await _postgres.StartAsync();
            pg = _postgres.GetConnectionString();
        }

        var mq = Environment.GetEnvironmentVariable("CASHFLOW_TEST_RABBITMQ");
        if (string.IsNullOrWhiteSpace(mq))
        {
            _rabbit = new RabbitMqBuilder("rabbitmq:4.1-management-alpine").Build();
            await _rabbit.StartAsync();
            mq = _rabbit.GetConnectionString();
        }

        PostgresAdminConnectionString = pg;
        RabbitMqUri = new Uri(mq);
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "co_test_" + Guid.NewGuid().ToString("N")[..12];
        await using (var conn = new NpgsqlConnection(PostgresAdminConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(PostgresAdminConnectionString) { Database = name, MaxPoolSize = 30, ConnectionIdleLifetime = 15 }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }

        if (_rabbit is not null)
        {
            await _rabbit.DisposeAsync();
        }
    }
}
