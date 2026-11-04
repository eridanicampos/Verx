using System.Globalization;
using System.Threading.RateLimiting;

namespace CashFlow.Consolidation.Api.Configuration;

internal static class RateLimitingSetup
{
    public const string ReadsPolicy = "reads";

    public static IServiceCollection AddCashFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitsPerSecond = configuration.GetValue("RateLimiting:ReadsPerSecondPerClient", 200);

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

            options.AddPolicy(ReadsPolicy, http =>
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
