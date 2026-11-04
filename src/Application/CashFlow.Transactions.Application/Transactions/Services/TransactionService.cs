using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Domain.Interfaces.Service;
using CashFlow.Transactions.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CashFlow.Transactions.Application.Transactions.Services;

public sealed partial class TransactionService : ITransactionService
{
    internal const int MaxConcurrencyAttempts = 5;

    internal const int MaxListLimit = 500;

    private readonly IUnitOfWork _uow;
    private readonly IOutbox _outbox;
    private readonly TimeProvider _clock;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(IUnitOfWork uow, IOutbox outbox, TimeProvider clock, ILogger<TransactionService> logger)
    {
        _uow = uow;
        _outbox = outbox;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<TransactionRegistration>> RegisterAsync(
        Guid accountId,
        TransactionType type,
        decimal amount,
        string? description,
        string idempotencyKey,
        string fingerprint,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxConcurrencyAttempts; attempt++)
        {
            var existing = await _uow.TransactionRepository.GetByIdempotencyKeyAsync(accountId, idempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return Replay(existing, fingerprint);
            }

            var account = await _uow.AccountRepository.GetByIdAsync(accountId, cancellationToken);
            if (account is null)
            {
                return AccountErrors.NotFound;
            }

            var now = _clock.GetUtcNow();
            var registered = account.Register(
                Guid.CreateVersion7(now),
                type,
                amount,
                description,
                idempotencyKey,
                fingerprint,
                now);

            if (registered.IsFailure)
            {
                return registered.Error!;
            }

            var transaction = registered.Value;
            _uow.TransactionRepository.Add(transaction);
            _outbox.Enqueue(new TransactionRegisteredV1(
                EventId: Guid.CreateVersion7(now),
                TransactionId: transaction.Id,
                AccountId: transaction.AccountId,
                Type: transaction.Type == TransactionType.Credit ? "credit" : "debit",
                Amount: transaction.Amount,
                Currency: "BRL",
                OccurredAt: transaction.OccurredAt));

            try
            {
                await _uow.CommitAsync(cancellationToken);
                return new TransactionRegistration(transaction, Replayed: false);
            }
            catch (ConcurrencyConflictException)
            {
                LogConcurrencyRetry(_logger, accountId, attempt);
                _uow.Reset();
                await Task.Delay(Backoff(attempt), _clock, cancellationToken);
            }
            catch (UniqueConstraintException)
            {
                _uow.Reset();
            }
        }

        return TransactionErrors.ConcurrencyExhausted;
    }

    public async Task<Result<IReadOnlyList<Transaction>>> ListByAccountAsync(
        Guid accountId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken)
    {
        if (await _uow.AccountRepository.GetByIdAsync(accountId, cancellationToken) is null)
        {
            return AccountErrors.NotFound;
        }

        var items = await _uow.TransactionRepository.ListByAccountAsync(
            accountId, from, to, Math.Clamp(limit, 1, MaxListLimit), cancellationToken);
        return Result.Success(items);
    }

    private static Result<TransactionRegistration> Replay(Transaction existing, string fingerprint) =>
        string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal)
            ? new TransactionRegistration(existing, Replayed: true)
            : TransactionErrors.IdempotencyKeyReused;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds((5 * Math.Pow(2, attempt)) + Random.Shared.Next(0, 10));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflito de concorrência na conta {AccountId}, tentativa {Attempt}")]
    private static partial void LogConcurrencyRetry(ILogger logger, Guid accountId, int attempt);
}
