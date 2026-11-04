using System.Net.Http.Headers;
using CashFlow.Consolidation.Application.Projections;
using CashFlow.Consolidation.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CashFlow.Consolidation.IntegrationTests.Infrastructure;

public sealed class ConsolidationApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly Uri _rabbitMq;
    private readonly bool _consumerEnabled;
    private readonly Func<IServiceProvider, IMessageDispatcher>? _dispatcherOverride;

    public ConsolidationApiFactory(
        string connectionString,
        Uri rabbitMq,
        bool consumerEnabled = true,
        Func<IServiceProvider, IMessageDispatcher>? dispatcherOverride = null)
    {
        _connectionString = connectionString;
        _rabbitMq = rabbitMq;
        _consumerEnabled = consumerEnabled;
        _dispatcherOverride = dispatcherOverride;
    }

    public string Suffix { get; } = Guid.NewGuid().ToString("N")[..10];

    public string Exchange => $"test.cashflow.transactions.{Suffix}";

    public string Queue => $"test.consolidation.{Suffix}";

    public const int MaxRetries = 2;

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public RabbitMqConnection Broker => Services.GetRequiredService<RabbitMqConnection>();

    public TransactionEventsConsumer Consumer => Services.GetRequiredService<TransactionEventsConsumer>();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        Npgsql.NpgsqlConnection.ClearAllPools();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var user = _rabbitMq.UserInfo.Split(':');
        builder.UseEnvironment("Testing");
        builder.UseSetting("SecretsPath", Path.Combine(AppContext.BaseDirectory, "no-secrets"));
        builder.UseSetting("ConnectionStrings:Consolidation", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Critical");
        builder.UseSetting("Auth:Authority", "http://identity.test");
        builder.UseSetting("Auth:RequireHttpsMetadata", "false");
        builder.UseSetting("RabbitMq:HostName", _rabbitMq.Host);
        builder.UseSetting("RabbitMq:Port", _rabbitMq.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:UserName", Uri.UnescapeDataString(user[0]));
        builder.UseSetting("RabbitMq:Password", Uri.UnescapeDataString(user[1]));
        builder.UseSetting("RabbitMq:SourceExchange", Exchange);
        builder.UseSetting("RabbitMq:Queue", Queue);
        builder.UseSetting("RabbitMq:MaxRetries", MaxRetries.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("RabbitMq:RetryDelay", "00:00:00.200");
        builder.UseSetting("RabbitMq:ConsumerEnabled", _consumerEnabled ? "true" : "false");
        builder.UseSetting("RateLimiting:ReadsPerSecondPerClient", "1000");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.Configuration = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                o.Configuration.SigningKeys.Add(TestTokens.Key);
                o.TokenValidationParameters.IssuerSigningKey = TestTokens.Key;
            });

            if (_dispatcherOverride is not null)
            {
                services.RemoveAll<IMessageDispatcher>();
                services.AddScoped(_dispatcherOverride);
            }
        });
    }

    public async Task WaitForConsumerAsync()
    {
        _ = Server;
        for (var i = 0; i < 100 && !Consumer.IsConsuming; i++)
        {
            await Task.Delay(100);
        }

        Consumer.IsConsuming.ShouldBeTrue("consumidor não iniciou");
    }
}
