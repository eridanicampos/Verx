using System.Threading.RateLimiting;
using CashFlow.Identity.Api.Application;
using CashFlow.Identity.Api.Domain;
using CashFlow.Identity.Api.Endpoints;
using CashFlow.Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SecretsPath relativo (launchSettings) é resolvido a partir do content root do projeto
var secretsPath = Path.GetFullPath(builder.Configuration["SecretsPath"] ?? "/run/secrets", builder.Environment.ContentRootPath);
builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: false);

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = 8 * 1024;
});

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.UseUtcTimestamp = true);

var connectionString = builder.Configuration.GetConnectionString("Identity")
    ?? throw new InvalidOperationException("ConnectionStrings:Identity não configurada.");

builder.Services.AddDbContext<IdentityDbContext>(o => o.UseNpgsql(
    connectionString,
    npgsql =>
    {
        npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema);
        npgsql.EnableRetryOnFailure(3);
    }));

builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("postgres", tags: ["ready"]);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var loginPermits = builder.Configuration.GetValue("RateLimiting:LoginPerMinutePerIp", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(AuthEndpoints.LoginRateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPermits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers.XFrameOptions = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAuthEndpoints();
app.MapDiscoveryEndpoints();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

_ = app.Services.GetRequiredService<SigningKeyProvider>();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
}

await UserSeeder.SeedAsync(app.Services, app.Configuration, CancellationToken.None);
await app.RunAsync();

public partial class Program;
