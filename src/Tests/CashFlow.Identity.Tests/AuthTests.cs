using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CashFlow.Identity.Tests;

public sealed class AuthTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private IdentityFactory _factory = null!;
    private HttpClient _client = null!;

    public AuthTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

    public async ValueTask InitializeAsync()
    {
        _factory = new IdentityFactory(await _pg.CreateDatabaseAsync());
        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private Task<HttpResponseMessage> LoginAsync(string user, string password, bool useCookie = false) =>
        _client.PostAsJsonAsync("/auth/token", new { username = user, password, useCookie });

    private Task<HttpResponseMessage> RefreshAsync(string token) =>
        _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = token });

    [Fact]
    public async Task Valid_login_issues_rs256_token_verifiable_with_published_jwks()
    {
        var response = await LoginAsync("operador", IdentityFactory.OperatorPassword);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;

        var jwks = new JsonWebKeySet(await _client.GetStringAsync("/.well-known/jwks.json"));
        jwks.Keys.ShouldHaveSingleItem().HasPrivateKey.ShouldBeFalse();
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(tokens.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = "cashflow-identity",
            ValidAudience = "cashflow-api",
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        });

        validation.IsValid.ShouldBeTrue();
        validation.Claims["scope"].ToString()!.ShouldContain("transactions:write");
        tokens.ExpiresIn.ShouldBeInRange(500, 600);
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Analyst_gets_read_only_scopes()
    {
        var raw = await LoginAsync("analista", IdentityFactory.AnalystPassword);
        raw.StatusCode.ShouldBe(HttpStatusCode.OK, await raw.Content.ReadAsStringAsync());
        var tokens = (await raw.Content.ReadFromJsonAsync<TokenResponse>())!;

        var jwt = new JsonWebToken(tokens.AccessToken);
        var scopes = jwt.GetClaim("scope").Value.Split(' ');
        scopes.ShouldNotContain("transactions:write");
        scopes.ShouldContain("balances:read");
    }

    [Fact]
    public async Task Wrong_password_and_unknown_user_are_indistinguishable_401()
    {
        var wrong = await LoginAsync("operador", "senha-errada");
        var unknown = await LoginAsync("ninguem", "senha-errada");

        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknown.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var a = await wrong.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var b = await unknown.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        a!["title"].ToString().ShouldBe(b!["title"].ToString());
        a["detail"].ToString().ShouldBe(b["detail"].ToString());
    }

    [Fact]
    public async Task Repeated_failures_lock_the_account_even_for_correct_password()
    {
        for (var i = 0; i < 5; i++)
        {
            await LoginAsync("analista", "errada");
        }

        (await LoginAsync("analista", IdentityFactory.AnalystPassword)).StatusCode.ShouldBe(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_of_old_token_revokes_whole_family()
    {
        var login = (await (await LoginAsync("operador", IdentityFactory.OperatorPassword)).Content.ReadFromJsonAsync<TokenResponse>())!;

        var rotated = await RefreshAsync(login.RefreshToken!);
        rotated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = (await rotated.Content.ReadFromJsonAsync<TokenResponse>())!;
        second.RefreshToken.ShouldNotBe(login.RefreshToken);

        (await RefreshAsync(login.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await RefreshAsync(second.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Revoked_token_cannot_be_refreshed()
    {
        var login = (await (await LoginAsync("operador", IdentityFactory.OperatorPassword)).Content.ReadFromJsonAsync<TokenResponse>())!;

        (await _client.PostAsJsonAsync("/auth/revoke", new { refreshToken = login.RefreshToken })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await RefreshAsync(login.RefreshToken!)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Web_clients_receive_refresh_token_only_in_hardened_cookie()
    {
        var response = await LoginAsync("operador", IdentityFactory.OperatorPassword, useCookie: true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        body.RefreshToken.ShouldBeNull();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("cf_refresh=", StringComparison.Ordinal)).ToLowerInvariant();
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=strict");
        cookie.ShouldContain("path=/auth");
    }

    [Fact]
    public async Task Garbage_refresh_token_is_401()
    {
        (await RefreshAsync("nao-existe")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Discovery_document_points_to_jwks()
    {
        var doc = await _client.GetFromJsonAsync<Dictionary<string, object>>("/.well-known/openid-configuration");

        doc!["issuer"].ToString().ShouldBe("cashflow-identity");
        doc["jwks_uri"].ToString()!.ShouldEndWith("/.well-known/jwks.json");
    }
}

public sealed class LoginRateLimitTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private IdentityFactory _factory = null!;

    public LoginRateLimitTests(PostgresFixture pg)
    {
        _pg = pg;
    }

    public async ValueTask InitializeAsync() => _factory = new IdentityFactory(await _pg.CreateDatabaseAsync(), loginPerMinute: 3);

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Login_is_rate_limited_per_ip()
    {
        var client = _factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.PostAsJsonAsync("/auth/token", new { username = "x", password = "y" })).StatusCode);
        }

        statuses.Take(3).ShouldAllBe(s => s == HttpStatusCode.Unauthorized);
        statuses.Skip(3).ShouldAllBe(s => s == HttpStatusCode.TooManyRequests);
    }
}
