using System.Net;
using System.Net.Http.Json;
using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Application.Projections;
using CashFlow.Consolidation.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class ProjectionTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private ConsolidationApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ProjectionTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new ConsolidationApiFactory(await _infra.CreateDatabaseAsync(), _infra.RabbitMqUri, consumerEnabled: false);
        _client = _factory.CreateClientWithToken(TestTokens.Analyst());
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task ProjectAsync(Guid accountId, string type, decimal amount, DateTimeOffset at, Guid? transactionId = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var projector = scope.ServiceProvider.GetRequiredService<TransactionRegisteredProjector>();
        await projector.ProjectAsync(
            Guid.NewGuid(),
            new TransactionRegisteredMessage(Guid.NewGuid(), transactionId ?? Guid.NewGuid(), accountId, type, amount, "BRL", at),
            CancellationToken.None);
    }

    [Fact]
    public async Task Balance_at_any_instant_and_daily_report()
    {
        var accountId = Guid.NewGuid();
        var d1 = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var d2 = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        await ProjectAsync(accountId, "credit", 1000m, d1);
        await ProjectAsync(accountId, "debit", 200m, d1.AddHours(2));
        await ProjectAsync(accountId, "debit", 50m, d2);
        await ProjectAsync(accountId, "credit", 25m, d2.AddHours(5));

        async Task<decimal> At(DateTimeOffset t) =>
            (await _client.GetFromJsonAsync<BalanceDto>($"/api/v1/accounts/{accountId}/balance?at={Uri.EscapeDataString(t.ToString("O"))}"))!.Balance;

        (await At(d1.AddMinutes(-1))).ShouldBe(0m);
        (await At(d1.AddHours(1))).ShouldBe(1000m);
        (await At(d1.AddHours(3))).ShouldBe(800m);
        (await At(d2.AddHours(1))).ShouldBe(750m);
        (await At(d2.AddHours(6))).ShouldBe(775m);

        var report = await _client.GetFromJsonAsync<List<DailyDto>>($"/api/v1/accounts/{accountId}/daily-balances?from=2026-10-01&to=2026-10-31");
        report!.Count.ShouldBe(2);
        report[0].ShouldBe(new DailyDto(new DateOnly(2026, 10, 1), 1000m, 200m, 800m, 800m, 2));
        report[1].ShouldBe(new DailyDto(new DateOnly(2026, 10, 2), 25m, 50m, -25m, 775m, 2));

        var fromSecondDay = await _client.GetFromJsonAsync<List<DailyDto>>($"/api/v1/accounts/{accountId}/daily-balances?from=2026-10-02&to=2026-10-02");
        fromSecondDay!.ShouldHaveSingleItem().ClosingBalance.ShouldBe(775m);
    }

    [Fact]
    public async Task Concurrent_projections_on_same_account_do_not_lose_updates()
    {
        var accountId = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => ProjectAsync(accountId, i % 2 == 0 ? "credit" : "debit", 1m + i, at.AddSeconds(i))));

        var expected = Enumerable.Range(0, 100).Sum(i => i % 2 == 0 ? 1m + i : -(1m + i));
        var balance = await _client.GetFromJsonAsync<BalanceDto>($"/api/v1/accounts/{accountId}/balance");
        balance!.Balance.ShouldBe(expected);
        var day = (await _client.GetFromJsonAsync<List<DailyDto>>($"/api/v1/accounts/{accountId}/daily-balances?from=2026-10-10&to=2026-10-10"))!.Single();
        day.EntryCount.ShouldBe(100);
    }

    [Fact]
    public async Task Out_of_order_events_yield_same_result()
    {
        var accountId = Guid.NewGuid();
        var t0 = new DateTimeOffset(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

        await ProjectAsync(accountId, "debit", 30m, t0.AddHours(2));
        await ProjectAsync(accountId, "credit", 100m, t0);

        (await _client.GetFromJsonAsync<BalanceDto>($"/api/v1/accounts/{accountId}/balance?at={Uri.EscapeDataString(t0.AddHours(1).ToString("O"))}"))!.Balance.ShouldBe(100m);
        (await _client.GetFromJsonAsync<BalanceDto>($"/api/v1/accounts/{accountId}/balance"))!.Balance.ShouldBe(70m);
    }

    [Fact]
    public async Task Unknown_account_is_404_and_invalid_range_is_400()
    {
        (await _client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}/balance")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}/daily-balances?from=2026-10-10&to=2026-10-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Authorization_is_enforced()
    {
        var accountId = Guid.NewGuid();
        await ProjectAsync(accountId, "credit", 1m, DateTimeOffset.UtcNow);

        (await _factory.CreateClient().GetAsync($"/api/v1/accounts/{accountId}/balance")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _factory.CreateClientWithToken(TestTokens.Create("transactions:write")).GetAsync($"/api/v1/accounts/{accountId}/balance"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClientWithToken(TestTokens.Create("balances:read", lifetime: TimeSpan.FromMinutes(-5))).GetAsync($"/api/v1/accounts/{accountId}/balance"))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
