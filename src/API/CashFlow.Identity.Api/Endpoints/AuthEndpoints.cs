using CashFlow.Identity.Api.Application;
using CashFlow.Identity.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace CashFlow.Identity.Api.Endpoints;

internal static class AuthEndpoints
{
    public const string RefreshCookie = "cf_refresh";
    public const string LoginRateLimitPolicy = "login";

    private static readonly string[] SigningAlgorithms = ["RS256"];
    private static readonly string[] ResponseTypes = ["token"];
    private static readonly string[] SubjectTypes = ["public"];

    public sealed record TokenRequest(string Username, string Password, bool UseCookie = false);

    public sealed record RefreshRequest(string? RefreshToken, bool UseCookie = false);

    public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Autenticação").AllowAnonymous();

        group.MapPost("/token", async (TokenRequest request, AuthService auth, HttpContext http, TimeProvider clock, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password) || request.Password.Length > 256)
                {
                    return Results.Problem(title: "auth.invalid_request", statusCode: StatusCodes.Status400BadRequest);
                }

                var result = await auth.LoginAsync(request.Username, request.Password, http.Connection.RemoteIpAddress?.ToString(), ct);
                return ToHttp(result, request.UseCookie, http, clock);
            })
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("IssueToken")
            .WithSummary("Autentica com usuário e senha e emite access token (JWT RS256) + refresh token");

        group.MapPost("/refresh", async (RefreshRequest request, AuthService auth, HttpContext http, TimeProvider clock, CancellationToken ct) =>
            {
                var token = request.RefreshToken ?? http.Request.Cookies[RefreshCookie];
                if (string.IsNullOrWhiteSpace(token))
                {
                    return Results.Problem(title: "auth.refresh_token_required", statusCode: StatusCodes.Status400BadRequest);
                }

                var useCookie = request.UseCookie || request.RefreshToken is null;
                var result = await auth.RefreshAsync(token, http.Connection.RemoteIpAddress?.ToString(), ct);
                return ToHttp(result, useCookie, http, clock);
            })
            .RequireRateLimiting(LoginRateLimitPolicy)
            .WithName("RefreshToken")
            .WithSummary("Rotaciona o refresh token. Reuso de token antigo revoga toda a sessão.");

        group.MapPost("/revoke", async (RefreshRequest request, AuthService auth, HttpContext http, CancellationToken ct) =>
            {
                var token = request.RefreshToken ?? http.Request.Cookies[RefreshCookie];
                if (!string.IsNullOrWhiteSpace(token))
                {
                    await auth.RevokeAsync(token, ct);
                }

                http.Response.Cookies.Delete(RefreshCookie, CookieOptions(DateTimeOffset.UnixEpoch));
                return Results.NoContent();
            })
            .WithName("RevokeToken")
            .WithSummary("Logout: revoga a cadeia de refresh tokens");

        return app;
    }

    public static IEndpointRouteBuilder MapDiscoveryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/.well-known/openid-configuration", (IOptions<JwtOptions> options) =>
            {
                var o = options.Value;
                return Results.Ok(new Dictionary<string, object>
                {
                    ["issuer"] = o.Issuer,
                    ["jwks_uri"] = $"{o.PublicBaseUrl.TrimEnd('/')}/.well-known/jwks.json",
                    ["token_endpoint"] = $"{o.PublicBaseUrl.TrimEnd('/')}/auth/token",
                    ["id_token_signing_alg_values_supported"] = SigningAlgorithms,
                    ["response_types_supported"] = ResponseTypes,
                    ["subject_types_supported"] = SubjectTypes,
                });
            })
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapGet("/.well-known/jwks.json", (SigningKeyProvider keys) =>
                Results.Ok(new { keys = new[] { keys.PublicJwk() } }))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return app;
    }

    private static IResult ToHttp(AuthResult result, bool useCookie, HttpContext http, TimeProvider clock)
    {
        if (result.Tokens is null)
        {
            return result.Failure switch
            {
                AuthFailure.LockedOut => Results.Problem(
                    title: "auth.locked_out",
                    detail: "Conta temporariamente bloqueada por excesso de tentativas.",
                    statusCode: StatusCodes.Status423Locked),
                _ => Results.Problem(
                    title: "auth.invalid_grant",
                    detail: "Credenciais ou token inválidos.",
                    statusCode: StatusCodes.Status401Unauthorized),
            };
        }

        var t = result.Tokens;
        if (useCookie)
        {
            http.Response.Cookies.Append(RefreshCookie, t.RefreshToken, CookieOptions(t.RefreshTokenExpiresAt));
        }

        http.Response.Headers.CacheControl = "no-store";
        var expiresIn = (int)Math.Max(0, (t.AccessTokenExpiresAt - clock.GetUtcNow()).TotalSeconds);
        return Results.Ok(new TokenResponse(t.AccessToken, "Bearer", expiresIn, useCookie ? null : t.RefreshToken, t.RefreshTokenExpiresAt));
    }

    private static CookieOptions CookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        Expires = expires,
        IsEssential = true,
    };
}
