using System.Security.Claims;
using System.Security.Cryptography;
using CashFlow.Identity.Api.Domain;
using CashFlow.Identity.Api.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Identity.Api.Application;

public sealed class TokenService
{
    private readonly SigningKeyProvider _keys;
    private readonly IOptions<JwtOptions> _options;
    private readonly TimeProvider _clock;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(SigningKeyProvider keys, IOptions<JwtOptions> options, TimeProvider clock)
    {
        _keys = keys;
        _options = options;
        _clock = clock;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user)
    {
        var o = _options.Value;
        var now = _clock.GetUtcNow();
        var expires = now + o.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = _keys.Credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("name", user.Username),
                new Claim("role", user.Role),
                new Claim("scope", string.Join(' ', Roles.ScopesOf(user.Role))),
            ]),
        };

        return (_handler.CreateToken(descriptor), expires);
    }

    public static (string Token, string Hash) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));
}
