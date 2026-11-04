using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Behaviors;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;
using CashFlow.Transactions.Application.Transactions.Services;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;
using CashFlow.Transactions.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace CashFlow.Transactions.UnitTests.Application;

public sealed class RegisterTransactionCommandHandlerTests
{
    private readonly InMemoryStore _store = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly Account _account;

    public RegisterTransactionCommandHandlerTests()
    {
        _clock.AutoAdvanceAmount = TimeSpan.FromMilliseconds(50);
        _account = Account.Open(Guid.NewGuid(), "Maria", "enc", "hash", 0, _clock.GetUtcNow()).Value;
        _store.Add(_account);
    }

    private ICommandHandler<RegisterTransactionCommand, RegisterTransactionResponse> CreateHandler() =>
        new ValidationBehavior<RegisterTransactionCommand, RegisterTransactionResponse>(
            new RegisterTransactionCommandHandler(new TransactionService(_store, _store, _clock, NullLogger<TransactionService>.Instance)),
            [new RegisterTransactionCommandValidator()]);

    private RegisterTransactionCommand Command(string key = "key-1", TransactionType type = TransactionType.Credit, decimal amount = 100m) =>
        new(_account.Id, type, amount, "Depósito", key);

    [Fact]
    public async Task Registers_transaction_and_enqueues_integration_event_atomically()
    {
        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Replayed.ShouldBeFalse();
        _store.Transactions.ShouldHaveSingleItem();
        var evt = _store.PublishedToOutbox.ShouldHaveSingleItem().ShouldBeOfType<TransactionRegisteredV1>();
        evt.TransactionId.ShouldBe(result.Value.Transaction.Id);
        evt.Type.ShouldBe("credit");
        evt.Currency.ShouldBe("BRL");
    }

    [Fact]
    public async Task Same_idempotency_key_and_payload_replays_original_result_without_new_effects()
    {
        var handler = CreateHandler();
        var first = await handler.HandleAsync(Command(), CancellationToken.None);

        var second = await handler.HandleAsync(Command(), CancellationToken.None);

        second.IsSuccess.ShouldBeTrue();
        second.Value.Replayed.ShouldBeTrue();
        second.Value.Transaction.Id.ShouldBe(first.Value.Transaction.Id);
        _store.Transactions.Count.ShouldBe(1);
        _store.PublishedToOutbox.Count.ShouldBe(1);
        _account.Balance.ShouldBe(100m);
    }

    [Fact]
    public async Task Same_idempotency_key_with_different_payload_is_conflict()
    {
        var handler = CreateHandler();
        await handler.HandleAsync(Command(amount: 100m), CancellationToken.None);

        var result = await handler.HandleAsync(Command(amount: 200m), CancellationToken.None);

        result.Error.ShouldBe(TransactionErrors.IdempotencyKeyReused);
    }

    [Fact]
    public async Task Concurrency_conflict_is_retried_and_succeeds()
    {
        _store.FailuresOnSave.Enqueue(new ConcurrencyConflictException("conflict"));
        _store.FailuresOnSave.Enqueue(new ConcurrencyConflictException("conflict"));

        var task = CreateHandler().HandleAsync(Command(), CancellationToken.None);
        while (!task.IsCompleted)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        var result = await task;
        result.IsSuccess.ShouldBeTrue();
        _store.SaveCalls.ShouldBe(3);
        _store.PublishedToOutbox.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Concurrency_retries_are_bounded()
    {
        for (var i = 0; i < TransactionService.MaxConcurrencyAttempts; i++)
        {
            _store.FailuresOnSave.Enqueue(new ConcurrencyConflictException("conflict"));
        }

        var task = CreateHandler().HandleAsync(Command(), CancellationToken.None);
        while (!task.IsCompleted)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        (await task).Error.ShouldBe(TransactionErrors.ConcurrencyExhausted);
        _store.PublishedToOutbox.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unique_violation_from_racing_duplicate_returns_winner_result()
    {
        var replica = Account.Open(_account.Id, "Maria", "enc", "hash", 0, _clock.GetUtcNow()).Value;
        var winner = replica.Register(Guid.NewGuid(), TransactionType.Credit, 100m, "Depósito", "key-1", Command().Fingerprint(), _clock.GetUtcNow()).Value;
        _store.FailuresOnSave.Enqueue(new UniqueConstraintException("dup"));
        _store.OnInjectedFailure = () => _store.Transactions.Add(winner);

        var result = await CreateHandler().HandleAsync(Command(), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Replayed.ShouldBeTrue();
        result.Value.Transaction.Id.ShouldBe(winner.Id);
        _store.PublishedToOutbox.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unknown_account_returns_not_found()
    {
        var result = await CreateHandler().HandleAsync(Command() with { AccountId = Guid.NewGuid() }, CancellationToken.None);

        result.Error.ShouldBe(AccountErrors.NotFound);
    }

    [Theory]
    [InlineData(0.001)]
    [InlineData(0)]
    [InlineData(10000000.01)]
    public async Task Invalid_amounts_fail_validation(decimal amount)
    {
        var result = await CreateHandler().HandleAsync(Command(amount: amount), CancellationToken.None);

        result.Error!.Code.ShouldBe("validation.failed");
    }

    [Fact]
    public async Task Invalid_idempotency_key_fails_validation()
    {
        var result = await CreateHandler().HandleAsync(Command(key: "chave com espaço"), CancellationToken.None);

        result.Error!.Code.ShouldBe("validation.failed");
    }

    [Fact]
    public void Fingerprint_is_stable_and_sensitive_to_payload()
    {
        Command().Fingerprint().ShouldBe(Command().Fingerprint());
        Command().Fingerprint().ShouldNotBe(Command(amount: 100.01m).Fingerprint());
        Command().Fingerprint().ShouldNotBe(Command(type: TransactionType.Debit).Fingerprint());
    }
}
