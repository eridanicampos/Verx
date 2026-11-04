using System.Text.Json;
using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.Infrastructure.Outbox;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class OutboxTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private readonly FakePublisher _publisher = new();
    private TransactionsApiFactory _factory = null!;
    private HttpClient _client = null!;

    public OutboxTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new TransactionsApiFactory(await _infra.CreateDatabaseAsync(), _publisher);
        _client = _factory.CreateClientWithToken(TestTokens.Operator());
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private Task<int> ProcessAsync() => _factory.Services.GetRequiredService<OutboxProcessor>().ProcessBatchAsync(CancellationToken.None);

    [Fact]
    public async Task Pending_events_are_published_with_stable_message_id_and_marked_processed()
    {
        var account = await _client.OpenAccountAsync();
        await _client.PostTransactionAsync(account.Id, "credit", 42m, "k-outbox");

        await ProcessAsync();

        var published = _publisher.Published.Where(m => m.Payload.Contains(account.Id.ToString())).ToList();
        published.Select(m => m.Type).ShouldBe(["account.opened.v1", "transaction.registered.v1"], ignoreOrder: true);
        var evt = JsonDocument.Parse(published.Single(m => m.Type == "transaction.registered.v1").Payload).RootElement;
        evt.GetProperty("amount").GetDecimal().ShouldBe(42m);
        evt.GetProperty("eventId").GetGuid().ShouldBe(published.Single(m => m.Type == "transaction.registered.v1").MessageId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        (await db.OutboxMessages.CountAsync(m => m.ProcessedAt == null)).ShouldBe(0);
    }

    [Fact]
    public async Task Broker_outage_does_not_block_writes_and_messages_are_retried_later()
    {
        _publisher.Fail = true;
        var account = await _client.OpenAccountAsync();
        (await _client.PostTransactionAsync(account.Id, "credit", 10m, "k-outage")).IsSuccessStatusCode.ShouldBeTrue();

        await ProcessAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
            var pending = await db.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync();
            pending.ShouldNotBeEmpty();
            pending.ShouldAllBe(m => m.Attempts == 1 && m.LastError != null && m.NextAttemptAt > m.CreatedAt);

            await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }

        _publisher.Fail = false;
        await ProcessAsync();

        _publisher.Published.Count(m => m.Payload.Contains(account.Id.ToString())).ShouldBe(2);
        await using var verify = _factory.Services.CreateAsyncScope();
        (await verify.ServiceProvider.GetRequiredService<TransactionsDbContext>().OutboxMessages.CountAsync(m => m.ProcessedAt == null)).ShouldBe(0);
    }

    [Fact]
    public async Task Concurrent_relays_do_not_publish_the_same_message_twice()
    {
        var account = await _client.OpenAccountAsync();
        for (var i = 0; i < 30; i++)
        {
            await _client.PostTransactionAsync(account.Id, "credit", 1m, $"k-par-{i}");
        }

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ProcessAsync()));
        await ProcessAsync();

        var ids = _publisher.Published.Where(m => m.Payload.Contains(account.Id.ToString())).Select(m => m.MessageId).ToList();
        ids.Count.ShouldBe(31);
        ids.Distinct().Count().ShouldBe(31);
    }
}
