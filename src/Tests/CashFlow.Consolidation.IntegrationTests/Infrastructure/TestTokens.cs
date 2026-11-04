using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Consolidation.IntegrationTests.Infrastructure;

public static class TestTokens
{
    public const string Issuer = "cashflow-identity";
    public const string Audience = "cashflow-api";

    public static readonly RsaSecurityKey Key = new(RSA.Create(2048)) { KeyId = "test-key" };
    public static readonly RsaSecurityKey OtherKey = new(RSA.Create(2048)) { KeyId = "test-key" };

    public static string Create(
        string scopes,
        string? subject = null,
        TimeSpan? lifetime = null,
        string audience = Audience,
        SecurityKey? signingKey = null)
    {
        var now = DateTime.UtcNow;
        var expires = now + (lifetime ?? TimeSpan.FromMinutes(10));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            IssuedAt = expires < now ? expires.AddMinutes(-1) : now,
            NotBefore = expires < now ? expires.AddMinutes(-1) : now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey ?? Key, SecurityAlgorithms.RsaSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", subject ?? Guid.NewGuid().ToString()),
                new Claim("scope", scopes),
            ]),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public static string Operator() => Create("transactions:write transactions:read balances:read");

    public static string Analyst() => Create("transactions:read balances:read");
}
