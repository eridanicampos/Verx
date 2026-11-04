using System.Net;
using System.Net.Http.Json;
using CashFlow.Transactions.IntegrationTests.Infrastructure;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class AuthorizationTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private TransactionsApiFactory _factory = null!;
    private AccountDto _account = null!;

    public AuthorizationTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new TransactionsApiFactory(await _infra.CreateDatabaseAsync(), new FakePublisher());
        _account = await _factory.CreateClientWithToken(TestTokens.Operator()).OpenAccountAsync();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Request_without_token_is_401()
    {
        var response = await _factory.CreateClient().PostTransactionAsync(_account.Id, "credit", 10, "k");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Read_only_scope_cannot_write_403()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.Analyst()).PostTransactionAsync(_account.Id, "credit", 10, "k");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Read_only_scope_can_read()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.Analyst()).GetAsync($"/api/v1/accounts/{_account.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Token_without_any_cashflow_scope_cannot_read_403()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.Create("other:scope")).GetAsync($"/api/v1/accounts/{_account.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Expired_token_is_401()
    {
        var token = TestTokens.Create("transactions:write", lifetime: TimeSpan.FromMinutes(-5));

        var response = await _factory.CreateClientWithToken(token).PostTransactionAsync(_account.Id, "credit", 10, "k");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_for_other_audience_is_401()
    {
        var token = TestTokens.Create("transactions:write", audience: "other-api");

        var response = await _factory.CreateClientWithToken(token).PostTransactionAsync(_account.Id, "credit", 10, "k");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_signed_with_unknown_key_is_401()
    {
        var token = TestTokens.Create("transactions:write", signingKey: TestTokens.OtherKey);

        var response = await _factory.CreateClientWithToken(token).PostTransactionAsync(_account.Id, "credit", 10, "k");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Health_endpoints_are_anonymous()
    {
        (await _factory.CreateClient().GetAsync("/health/live")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Responses_carry_security_headers_and_no_server_banner()
    {
        var response = await _factory.CreateClientWithToken(TestTokens.Analyst()).GetAsync($"/api/v1/accounts/{_account.Id}");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.Contains("Server").ShouldBeFalse();
        var body = await response.Content.ReadFromJsonAsync<AccountDto>(ApiClientExtensions.Json);
        body!.HolderDocumentMasked.ShouldStartWith("*");
    }
}
