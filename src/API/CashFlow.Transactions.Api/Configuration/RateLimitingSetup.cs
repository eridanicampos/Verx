using System.Globalization;
using System.Threading.RateLimiting;

namespace CashFlow.Transactions.Api.Configuration;

internal static class RateLimitingSetup
{
    public const string WritesPolicy = "writes";

    public static IServiceCollection AddCashFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerSecond = configuration.GetValue("RateLimiting:WritesPerSecondPerClient", 100);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                return ValueTask.CompletedTask;
            };

            options.AddPolicy(WritesPolicy, http =>
            {
                var partition = http.User.FindFirst("sub")?.Value
                    ?? http.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";
                return RateLimitPartition.GetTokenBucketLimiter(partition, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = permitsPerSecond * 2,
                    TokensPerPeriod = permitsPerSecond,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
            });
        });

        return services;
    }
}
