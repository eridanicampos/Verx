using System.Net;
using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class ConsistencyTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private TransactionsApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ConsistencyTests(TestInfrastructure infra)
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
    public async Task Parallel_debits_never_break_balance_invariant()
    {
        var account = await _client.OpenAccountAsync(overdraft: 0);
        (await _client.PostTransactionAsync(account.Id, "credit", 100m, "deposito")).StatusCode.ShouldBe(HttpStatusCode.Created);

        var responses = await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(i => _client.PostTransactionAsync(account.Id, "debit", 10m, $"debito-{i}")));

        responses.ShouldNotContain(r => (int)r.StatusCode >= 500);
        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        succeeded.ShouldBeLessThanOrEqualTo(10);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        var stored = await db.Accounts.SingleAsync(a => a.Id == account.Id);
        var ledger = await db.Transactions.Where(t => t.AccountId == account.Id).ToListAsync();

        stored.Balance.ShouldBe(100m - (10m * succeeded));
        stored.Balance.ShouldBeGreaterThanOrEqualTo(0m);
        ledger.Sum(t => t.SignedAmount).ShouldBe(stored.Balance);
        (await db.CountOutboxAsync(account.Id, "transaction.registered.v1")).ShouldBe(ledger.Count);
    }

    [Fact]
    public async Task Debit_beyond_limit_is_422_and_has_no_side_effects()
    {
        var account = await _client.OpenAccountAsync(overdraft: 50);

        var response = await _client.PostTransactionAsync(account.Id, "debit", 50.01m, "d1");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        (await db.Transactions.AnyAsync(t => t.AccountId == account.Id)).ShouldBeFalse();
        (await db.CountOutboxAsync(account.Id, "transaction.registered.v1")).ShouldBe(0);
    }

    [Fact]
    public async Task Unknown_account_is_404_and_invalid_type_is_400()
    {
        (await _client.PostTransactionAsync(Guid.NewGuid(), "credit", 1m, "x")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var account = await _client.OpenAccountAsync();
        (await _client.PostTransactionAsync(account.Id, "transfer", 1m, "x")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Holder_document_is_encrypted_at_rest()
    {
        var account = await _client.OpenAccountAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        var stored = await db.Accounts.SingleAsync(a => a.Id == account.Id);
        stored.HolderDocument.ShouldStartWith("k1:");
        stored.HolderDocument.ShouldNotContain("123");
    }
}
