using System.Net.Http.Headers;
using System.Security.Cryptography;
using CashFlow.Transactions.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CashFlow.Transactions.IntegrationTests.Infrastructure;

public sealed class TransactionsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly IMessagePublisher? _publisher;
    private readonly Uri? _rabbitMq;

    public TransactionsApiFactory(string connectionString, IMessagePublisher? publisher = null, Uri? rabbitMq = null)
    {
        _connectionString = connectionString;
        _publisher = publisher;
        _rabbitMq = rabbitMq;
    }

    public HttpClient CreateClientWithToken(string token)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
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
        builder.UseSetting("ConnectionStrings:Transactions", _connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Critical");
        builder.UseSetting("Outbox:Enabled", "false");
        builder.UseSetting("Auth:Authority", "http://identity.test");
        builder.UseSetting("Auth:RequireHttpsMetadata", "false");
        builder.UseSetting("Pii:ActiveKeyId", "k1");
        builder.UseSetting("Pii:EncryptionKeys:k1", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Pii:BlindIndexKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("RateLimiting:WritesPerSecondPerClient", "1000");

        if (_rabbitMq is not null)
        {
            var userInfo = _rabbitMq.UserInfo.Split(':');
            builder.UseSetting("RabbitMq:HostName", _rabbitMq.Host);
            builder.UseSetting("RabbitMq:Port", _rabbitMq.Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("RabbitMq:UserName", Uri.UnescapeDataString(userInfo[0]));
            builder.UseSetting("RabbitMq:Password", Uri.UnescapeDataString(userInfo[1]));
        }

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            {
                o.Authority = null;
                o.Configuration = new OpenIdConnectConfiguration { Issuer = TestTokens.Issuer };
                o.Configuration.SigningKeys.Add(TestTokens.Key);
                o.TokenValidationParameters.IssuerSigningKey = TestTokens.Key;
            });

            if (_publisher is not null)
            {
                services.RemoveAll<IMessagePublisher>();
                services.AddSingleton(_publisher);
            }
        });
    }
}
