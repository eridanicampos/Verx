using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Gateway.Tests;

public sealed class GatewayTests : IAsyncLifetime
{
    private readonly List<WebApplication> _stubs = [];
    private readonly Dictionary<string, string> _addresses = [];

    public async ValueTask InitializeAsync()
    {
        foreach (var name in new[] { "identity", "transactions", "consolidation" })
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build();
            app.Run(ctx =>
            {
                ctx.Response.Headers["X-Downstream"] = name;
                return ctx.Response.WriteAsync(ctx.Request.Path);
            });
            await app.StartAsync();
            _addresses[name] = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            _stubs.Add(app);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var stub in _stubs)
        {
            await stub.DisposeAsync();
        }
    }

    private WebApplicationFactory<Program> Gateway(int perSecond = 1000, int burst = 1000, int authPerMinute = 1000, string? frontDoorId = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            if (frontDoorId is not null)
            {
                b.UseSetting("Edge:RequiredFrontDoorId", frontDoorId);
            }

            b.UseSetting("ReverseProxy:Clusters:identity:Destinations:d1:Address", _addresses["identity"]);
            b.UseSetting("ReverseProxy:Clusters:transactions:Destinations:d1:Address", _addresses["transactions"]);
            b.UseSetting("ReverseProxy:Clusters:consolidation:Destinations:d1:Address", _addresses["consolidation"]);
            b.UseSetting("RateLimiting:RequestsPerSecondPerIp", perSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("RateLimiting:BurstPerIp", burst.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("RateLimiting:AuthRequestsPerMinutePerIp", authPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("Logging:LogLevel:Default", "Warning");
        });

    [Theory]
    [InlineData("/auth/token", "identity")]
    [InlineData("/.well-known/jwks.json", "identity")]
    [InlineData("/api/v1/accounts/0192f3a1-0000-7000-8000-bbbbbbbbbbbb/balance", "consolidation")]
    [InlineData("/api/v1/accounts/0192f3a1-0000-7000-8000-bbbbbbbbbbbb/daily-balances", "consolidation")]
    [InlineData("/api/v1/accounts/0192f3a1-0000-7000-8000-bbbbbbbbbbbb/transactions", "transactions")]
    [InlineData("/api/v1/accounts", "transactions")]
    public async Task Routes_to_the_owning_service(string path, string expected)
    {
        await using var gateway = Gateway();

        var response = await gateway.CreateClient().GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("X-Downstream").ShouldContain(expected);
    }

    [Fact]
    public async Task Global_rate_limit_rejects_flood_with_429_and_retry_after()
    {
        await using var gateway = Gateway(perSecond: 5, burst: 5);
        var client = gateway.CreateClient();

        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < 12; i++)
        {
            responses.Add(await client.GetAsync("/api/v1/accounts"));
        }

        responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests).ShouldBeGreaterThan(0);
        var rejected = responses.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        rejected.Headers.Contains("Retry-After").ShouldBeTrue();
    }

    [Fact]
    public async Task Auth_endpoints_have_stricter_limit_against_brute_force()
    {
        await using var gateway = Gateway(authPerMinute: 3);
        var client = gateway.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            statuses.Add((await client.PostAsync("/auth/token", new StringContent("{}"))).StatusCode);
        }

        statuses.Count(s => s == HttpStatusCode.TooManyRequests).ShouldBe(2);
        (await client.GetAsync("/api/v1/accounts")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_is_not_rate_limited_and_security_headers_are_set()
    {
        await using var gateway = Gateway(perSecond: 1, burst: 1);
        var client = gateway.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var response = await client.GetAsync("/health/live");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
    }

    [Fact]
    public async Task Oversized_body_is_rejected()
    {
        await using var gateway = Gateway();
        var client = gateway.CreateClient();

        var response = await client.PostAsync("/api/v1/accounts", new ByteArrayContent(new byte[128 * 1024]));

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task When_behind_front_door_requests_bypassing_the_waf_are_rejected()
    {
        var id = Guid.NewGuid().ToString();
        await using var gateway = Gateway(frontDoorId: id);
        var client = gateway.CreateClient();

        (await client.GetAsync("/api/v1/accounts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var viaFrontDoor = new HttpRequestMessage(HttpMethod.Get, "/api/v1/accounts");
        viaFrontDoor.Headers.Add("X-Azure-FDID", id);
        (await client.SendAsync(viaFrontDoor)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
