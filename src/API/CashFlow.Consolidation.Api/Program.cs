using CashFlow.Consolidation.Api.Configuration;
using CashFlow.Consolidation.Application.Configuration;
using CashFlow.Consolidation.Infrastructure.Configurations;

var builder = WebApplication.CreateBuilder(args);

// SecretsPath relativo (launchSettings) é resolvido a partir do content root do projeto
var secretsPath = Path.GetFullPath(builder.Configuration["SecretsPath"] ?? "/run/secrets", builder.Environment.ContentRootPath);
builder.Configuration.AddKeyPerFile(secretsPath, optional: true, reloadOnChange: false);

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = 16 * 1024;
});

builder.AddCashFlowObservability("cashflow-consolidation");

builder.Services
    .AddApiConfig()
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddCashFlowAuthentication(builder.Configuration)
    .AddCashFlowRateLimiting(builder.Configuration);

var app = builder.Build();

app.UseApiConfig();

await app.MigrateIfEnabledAsync();
await app.RunAsync();

public partial class Program;
