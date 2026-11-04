using System.Net;
using System.Net.Http.Json;
using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class IdempotencyTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private TransactionsApiFactory _factory = null!;
    private HttpClient _client = null!;

    public IdempotencyTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new TransactionsApiFactory(await _infra.CreateDatabaseAsync(), new FakePublisher());
        _client = _factory.CreateClientWithToken(TestTokens.Operator());
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Retrying_with_same_key_returns_same_transaction_and_does_not_duplicate()
    {
        var account = await _client.OpenAccountAsync();

        var first = await _client.PostTransactionAsync(account.Id, "credit", 100m, "pagamento-123");
        var second = await _client.PostTransactionAsync(account.Id, "credit", 100m, "pagamento-123");

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        first.Headers.GetValues("Idempotency-Replayed").ShouldContain("false");
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.Headers.GetValues("Idempotency-Replayed").ShouldContain("true");
        (await second.Content.ReadFromJsonAsync<TransactionDto>(ApiClientExtensions.Json))!.Id
            .ShouldBe((await first.Content.ReadFromJsonAsync<TransactionDto>(ApiClientExtensions.Json))!.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        (await db.Transactions.CountAsync(t => t.AccountId == account.Id)).ShouldBe(1);
        (await db.CountOutboxAsync(account.Id, "transaction.registered.v1")).ShouldBe(1);
        (await db.Accounts.SingleAsync(a => a.Id == account.Id)).Balance.ShouldBe(100m);
    }

    [Fact]
    public async Task Same_key_with_different_payload_is_409()
    {
        var account = await _client.OpenAccountAsync();
        await _client.PostTransactionAsync(account.Id, "credit", 100m, "k-1");

        var response = await _client.PostTransactionAsync(account.Id, "credit", 999m, "k-1");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Missing_idempotency_key_is_400()
    {
        var account = await _client.OpenAccountAsync();

        var response = await _client.PostTransactionAsync(account.Id, "credit", 100m, idempotencyKey: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_requests_with_same_key_produce_exactly_one_effect()
    {
        var account = await _client.OpenAccountAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => _client.PostTransactionAsync(account.Id, "credit", 10m, "rajada-1")));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict);
        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        (await db.Transactions.CountAsync(t => t.AccountId == account.Id)).ShouldBe(1);
        (await db.Accounts.SingleAsync(a => a.Id == account.Id)).Balance.ShouldBe(10m);
    }
}
