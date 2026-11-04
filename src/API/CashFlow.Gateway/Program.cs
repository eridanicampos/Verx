using CashFlow.Gateway;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
const long MaxBodyBytes = 64 * 1024;

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = MaxBodyBytes;
    k.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
});

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.UseUtcTimestamp = true);

builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddGatewayRateLimiting(builder.Configuration);
builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Authorization", "Content-Type", "Idempotency-Key")
    .WithExposedHeaders("Idempotency-Replayed", "Retry-After")
    .AllowCredentials()));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.Use(async (ctx, next) =>
{
    if (ctx.Request.ContentLength > MaxBodyBytes)
    {
        ctx.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }

    var headers = ctx.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
    if (ctx.Request.IsHttps)
    {
        headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
    }

    await next();
});
var frontDoorId = app.Configuration["Edge:RequiredFrontDoorId"];
if (!string.IsNullOrWhiteSpace(frontDoorId))
{
    app.Use(async (ctx, next) =>
    {
        if (!ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(ctx.Request.Headers["X-Azure-FDID"], frontDoorId, StringComparison.OrdinalIgnoreCase))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        await next();
    });
}

app.UseCors();
app.UseRateLimiter();

app.MapHealthChecks("/health/live").DisableRateLimiting();
app.MapHealthChecks("/health/ready").DisableRateLimiting();
app.MapReverseProxy();

await app.RunAsync();

public partial class Program;
