using System.Globalization;
using System.Threading.RateLimiting;

namespace CashFlow.Gateway;

internal static class GatewayRateLimiting
{
    public const string AuthPolicy = "auth";

    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var perIp = configuration.GetValue("RateLimiting:RequestsPerSecondPerIp", 100);
        var burst = configuration.GetValue("RateLimiting:BurstPerIp", 200);
        var authPerMinute = configuration.GetValue("RateLimiting:AuthRequestsPerMinutePerIp", 20);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsync(
                    """{"title":"rate_limit.exceeded","status":429,"detail":"Muitas requisições. Tente novamente mais tarde."}""",
                    ct);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetTokenBucketLimiter(
                    ClientKey(http),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = burst,
                        TokensPerPeriod = perIp,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(AuthPolicy, http =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(http),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string ClientKey(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
