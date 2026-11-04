using System.Text;
using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Application.Projections;
using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CashFlow.Consolidation.UnitTests;

public sealed class ProjectorTests
{
    private readonly FakeUnitOfWork _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));

    private MessageDispatcher Dispatcher() => new(
        new TransactionRegisteredProjector(_store, _clock, NullLogger<TransactionRegisteredProjector>.Instance),
        new AccountOpenedProjector(_store));

    private static TransactionRegisteredMessage Message(string type = "credit", decimal amount = 10m, string currency = "BRL") =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), type, amount, currency, DateTimeOffset.UtcNow);

    [Fact]
    public async Task Same_message_id_is_processed_only_once()
    {
        var projector = new TransactionRegisteredProjector(_store, _clock, NullLogger<TransactionRegisteredProjector>.Instance);
        var messageId = Guid.NewGuid();
        var message = Message();

        (await projector.ProjectAsync(messageId, message, CancellationToken.None)).ShouldBe(MessageOutcome.Processed);
        (await projector.ProjectAsync(messageId, message, CancellationToken.None)).ShouldBe(MessageOutcome.Duplicate);

        _store.Entries.ShouldHaveSingleItem();
        _store.DailyApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Same_transaction_with_new_message_id_is_not_double_counted()
    {
        var projector = new TransactionRegisteredProjector(_store, _clock, NullLogger<TransactionRegisteredProjector>.Instance);
        var message = Message();

        await projector.ProjectAsync(Guid.NewGuid(), message, CancellationToken.None);
        (await projector.ProjectAsync(Guid.NewGuid(), message, CancellationToken.None)).ShouldBe(MessageOutcome.Duplicate);

        _store.DailyApplications.ShouldBe(1);
    }

    [Fact]
    public async Task Debit_is_projected_as_negative_amount()
    {
        await Dispatcher().DispatchAsync(Guid.NewGuid(), TransactionRegisteredMessage.MessageType, Json(Message("debit", 30m)), CancellationToken.None);

        _store.Entries.ShouldHaveSingleItem().SignedAmount.ShouldBe(-30m);
    }

    [Theory]
    [InlineData("transfer", 10, "BRL")]
    [InlineData("credit", 0, "BRL")]
    [InlineData("credit", 10, "USD")]
    public async Task Invalid_payloads_are_permanent_failures(string type, decimal amount, string currency)
    {
        await Should.ThrowAsync<PermanentMessageException>(() =>
            Dispatcher().DispatchAsync(Guid.NewGuid(), TransactionRegisteredMessage.MessageType, Json(Message(type, amount, currency)), CancellationToken.None));
        _store.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Malformed_json_is_permanent_failure()
    {
        await Should.ThrowAsync<PermanentMessageException>(() =>
            Dispatcher().DispatchAsync(Guid.NewGuid(), TransactionRegisteredMessage.MessageType, Encoding.UTF8.GetBytes("{not json"), CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_message_type_is_permanent_failure()
    {
        await Should.ThrowAsync<PermanentMessageException>(() =>
            Dispatcher().DispatchAsync(Guid.NewGuid(), "something.v9", Encoding.UTF8.GetBytes("{}"), CancellationToken.None));
    }

    [Fact]
    public async Task Account_opened_registers_account()
    {
        var accountId = Guid.NewGuid();
        var payload = Encoding.UTF8.GetBytes($$"""{"eventId":"{{Guid.NewGuid()}}","accountId":"{{accountId}}","occurredAt":"2026-10-29T10:00:00-03:00"}""");

        (await Dispatcher().DispatchAsync(Guid.NewGuid(), AccountOpenedMessage.MessageType, payload, CancellationToken.None)).ShouldBe(MessageOutcome.Processed);

        _store.Accounts.ShouldContainKey(accountId);
        _store.Accounts[accountId].Offset.ShouldBe(TimeSpan.Zero);
    }

    private static readonly System.Text.Json.JsonSerializerOptions WebJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    private static byte[] Json(TransactionRegisteredMessage m) =>
        Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(m, WebJson));

    private sealed class FakeUnitOfWork
        : IUnitOfWork, IInboxRepository, ILedgerEntryRepository, IDailyBalanceRepository, IConsolidatedAccountRepository
    {
        private readonly HashSet<Guid> _inbox = [];

        public List<LedgerEntry> Entries { get; } = [];

        public int DailyApplications { get; private set; }

        public Dictionary<Guid, DateTimeOffset> Accounts { get; } = [];

        public IBalanceReadRepository BalanceReadRepository => throw new NotSupportedException();

        public IInboxRepository InboxRepository => this;

        public ILedgerEntryRepository LedgerEntryRepository => this;

        public IDailyBalanceRepository DailyBalanceRepository => this;

        public IConsolidatedAccountRepository ConsolidatedAccountRepository => this;

        public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
            work(cancellationToken);

        public Task<bool> TryRegisterAsync(Guid messageId, string messageType, CancellationToken cancellationToken) =>
            Task.FromResult(_inbox.Add(messageId));

        public Task<bool> TryAddAsync(LedgerEntry entry, CancellationToken cancellationToken)
        {
            if (Entries.Any(e => e.TransactionId == entry.TransactionId))
            {
                return Task.FromResult(false);
            }

            Entries.Add(entry);
            return Task.FromResult(true);
        }

        public Task ApplyAsync(LedgerEntry entry, DateTimeOffset now, CancellationToken cancellationToken)
        {
            DailyApplications++;
            return Task.CompletedTask;
        }

        public Task UpsertAsync(Guid accountId, DateTimeOffset openedAt, DateTimeOffset? lastEntryAt, CancellationToken cancellationToken)
        {
            Accounts[accountId] = openedAt;
            return Task.CompletedTask;
        }
    }
}
