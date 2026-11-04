using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CashFlow.Identity.Tests.PostgresFixture))]

namespace CashFlow.Identity.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string AdminConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var pg = Environment.GetEnvironmentVariable("CASHFLOW_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(pg))
        {
            _container = new PostgreSqlBuilder("postgres:17-alpine").WithCommand("-c", "max_connections=500").Build();
            await _container.StartAsync();
            pg = _container.GetConnectionString();
        }

        AdminConnectionString = pg;
    }

    public async Task<string> CreateDatabaseAsync()
    {
        var name = "id_test_" + Guid.NewGuid().ToString("N")[..12];
        await using var conn = new NpgsqlConnection(AdminConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE {name}", conn);
        await cmd.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = name, MaxPoolSize = 30, ConnectionIdleLifetime = 15 }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public sealed class IdentityFactory : WebApplicationFactory<Program>
{
    public const string OperatorPassword = "Senha-Forte-Operador-123";
    public const string AnalystPassword = "Senha-Forte-Analista-456";

    private static readonly string Pem = CreatePem();

    private readonly string _connectionString;
    private readonly int _loginPerMinute;

    public IdentityFactory(string connectionString, int loginPerMinute = 1000)
    {
        _connectionString = connectionString;
        _loginPerMinute = loginPerMinute;
    }

    private static string CreatePem()
    {
        using var rsa = RSA.Create(2048);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        Npgsql.NpgsqlConnection.ClearAllPools();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("SecretsPath", Path.Combine(AppContext.BaseDirectory, "no-secrets"));
        builder.UseSetting("ConnectionStrings:Identity", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Critical");
        builder.UseSetting("Jwt:SigningKeyPem", Pem);
        builder.UseSetting("Jwt:PublicBaseUrl", "http://localhost");
        builder.UseSetting("Identity:SeedUsers:0:Password", OperatorPassword);
        builder.UseSetting("Identity:SeedUsers:1:Password", AnalystPassword);
        builder.UseSetting("RateLimiting:LoginPerMinutePerIp", _loginPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
