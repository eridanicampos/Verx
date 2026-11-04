using System.Net;
using System.Net.Http.Json;
using System.Text;
using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Application.Projections;
using CashFlow.Consolidation.Infrastructure.Context;
using CashFlow.Consolidation.Infrastructure.Repository;
using CashFlow.Consolidation.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Consolidation.IntegrationTests;

public sealed class MessagingTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private ConsolidationApiFactory _factory = null!;
    private HttpClient _client = null!;

    public MessagingTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new ConsolidationApiFactory(await _infra.CreateDatabaseAsync(), _infra.RabbitMqUri);
        await _factory.WaitForConsumerAsync();
        _client = _factory.CreateClientWithToken(TestTokens.Analyst());
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    private async Task<BalanceDto?> BalanceAsync(Guid accountId)
    {
        var response = await _client.GetAsync($"/api/v1/accounts/{accountId}/balance");
        return response.StatusCode == HttpStatusCode.OK ? await response.Content.ReadFromJsonAsync<BalanceDto>() : null;
    }

    [Fact]
    public async Task Published_events_are_consolidated_and_exposed_by_api()
    {
        var accountId = Guid.NewGuid();
        await _factory.PublishAsync("account.opened.v1", Events.AccountOpened(accountId));
        await _factory.PublishAsync("transaction.registered.v1", Events.Transaction(accountId, "credit", 300m));
        await _factory.PublishAsync("transaction.registered.v1", Events.Transaction(accountId, "debit", 120.50m));

        var balance = await BrokerTestExtensions.EventuallyAsync(() => BalanceAsync(accountId), b => b?.Balance == 179.50m);

        balance.ShouldNotBeNull();
        balance.Balance.ShouldBe(179.50m);
        balance.TotalCredits.ShouldBe(300m);
        balance.TotalDebits.ShouldBe(120.50m);
    }

    [Fact]
    public async Task Redelivered_message_is_deduplicated_by_inbox()
    {
        var accountId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var evt = Events.Transaction(accountId, "credit", 50m);

        for (var i = 0; i < 3; i++)
        {
            await _factory.PublishAsync("transaction.registered.v1", evt, messageId);
        }

        await _factory.PublishAsync("transaction.registered.v1", Events.Transaction(accountId, "credit", 1m));
        var balance = await BrokerTestExtensions.EventuallyAsync(() => BalanceAsync(accountId), b => b?.Balance == 51m);
        await Task.Delay(500);
        balance = await BalanceAsync(accountId);

        balance!.Balance.ShouldBe(51m);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        (await db.InboxMessages.CountAsync(m => m.MessageId == messageId)).ShouldBe(1);
        (await db.LedgerEntries.CountAsync(e => e.AccountId == accountId)).ShouldBe(2);
    }

    [Fact]
    public async Task Poison_message_goes_straight_to_dlq_without_retries()
    {
        var messageId = Guid.NewGuid();
        await _factory.PublishRawAsync("transaction.registered.v1", Encoding.UTF8.GetBytes("{ isto não é json"), messageId);

        var dead = await _factory.WaitForDeadLetterAsync(TimeSpan.FromSeconds(10));

        dead.ShouldNotBeNull();
        dead.BasicProperties.MessageId.ShouldBe(messageId.ToString());
        dead.RetryCount().ShouldBe(0);
        dead.BasicProperties.Headers!.ShouldContainKey("x-death");
    }

    [Fact]
    public async Task Business_invalid_message_goes_to_dlq_and_is_not_projected()
    {
        var accountId = Guid.NewGuid();
        await _factory.PublishAsync("transaction.registered.v1", Events.Transaction(accountId, "credit", -10m));

        var dead = await _factory.WaitForDeadLetterAsync(TimeSpan.FromSeconds(10));

        dead.ShouldNotBeNull();
        (await BalanceAsync(accountId)).ShouldBeNull();
    }

    [Fact]
    public async Task Unknown_message_type_goes_to_dlq()
    {
        var conn = await _factory.Broker.GetAsync(CancellationToken.None);
        await using var channel = await conn.CreateChannelAsync();
        await channel.BasicPublishAsync(string.Empty, _factory.Queue, mandatory: true,
            new RabbitMQ.Client.BasicProperties { MessageId = Guid.NewGuid().ToString(), Type = "account.closed.v7" },
            Encoding.UTF8.GetBytes("{}"));

        (await _factory.WaitForDeadLetterAsync(TimeSpan.FromSeconds(10))).ShouldNotBeNull();
    }

    [Fact]
    public async Task Missing_message_id_goes_to_dlq()
    {
        var conn = await _factory.Broker.GetAsync(CancellationToken.None);
        await using var channel = await conn.CreateChannelAsync();
        await channel.BasicPublishAsync(string.Empty, _factory.Queue, mandatory: true,
            new RabbitMQ.Client.BasicProperties { Type = "transaction.registered.v1" },
            Encoding.UTF8.GetBytes("{}"));

        (await _factory.WaitForDeadLetterAsync(TimeSpan.FromSeconds(10))).ShouldNotBeNull();
    }
}

public sealed class RetryAndDeadLetterTests : IAsyncLifetime
{
    private readonly TestInfrastructure _infra;
    private readonly FlakyDispatcher _flaky = new();
    private ConsolidationApiFactory _factory = null!;

    public RetryAndDeadLetterTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    public async ValueTask InitializeAsync()
    {
        _factory = new ConsolidationApiFactory(await _infra.CreateDatabaseAsync(), _infra.RabbitMqUri, dispatcherOverride: _ => _flaky);
        await _factory.WaitForConsumerAsync();
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();

    [Fact]
    public async Task Transient_failure_is_retried_then_dead_lettered_with_retry_count()
    {
        _flaky.FailuresBeforeSuccess = int.MaxValue;
        var messageId = Guid.NewGuid();

        await _factory.PublishAsync("transaction.registered.v1", Events.Transaction(Guid.NewGuid(), "credit", 10m), messageId);
        var dead = await _factory.WaitForDeadLetterAsync(TimeSpan.FromSeconds(15));

        dead.ShouldNotBeNull();
        dead.BasicProperties.MessageId.ShouldBe(messageId.ToString());
        dead.RetryCount().ShouldBe(ConsolidationApiFactory.MaxRetries);
        _flaky.Attempts.ShouldBe(ConsolidationApiFactory.MaxRetries + 1);
    }

    [Fact]
    public async Task Transient_failure_recovers_within_retry_budget()
    {
        _flaky.FailuresBeforeSuccess = 1;

        await _factory.PublishAsync("transaction.registered.v1", Events.Transaction(Guid.NewGuid(), "credit", 10m));
        await BrokerTestExtensions.EventuallyAsync(() => Task.FromResult(_flaky.Successes), s => s == 1);

        _flaky.Successes.ShouldBe(1);
        (await _factory.WaitForDeadLetterAsync(TimeSpan.FromSeconds(1))).ShouldBeNull();
    }

    private sealed class FlakyDispatcher : IMessageDispatcher
    {
        private int _attempts;
        private int _successes;

        public int FailuresBeforeSuccess { get; set; }

        public int Attempts => Volatile.Read(ref _attempts);

        public int Successes => Volatile.Read(ref _successes);

        public Task<MessageOutcome> DispatchAsync(Guid messageId, string? messageType, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _attempts) <= FailuresBeforeSuccess)
            {
                throw new TimeoutException("Banco indisponível (simulado).");
            }

            Interlocked.Increment(ref _successes);
            return Task.FromResult(MessageOutcome.Processed);
        }
    }
}
