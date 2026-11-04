namespace CashFlow.Identity.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "cashflow-identity";

    public string Audience { get; set; } = "cashflow-api";

    public string PublicBaseUrl { get; set; } = "http://localhost:5003";

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);

    public string SigningKeyPem { get; set; } = string.Empty;

    public bool AllowEphemeralKey { get; set; }
}
