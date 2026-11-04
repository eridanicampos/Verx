using CashFlow.Transactions.Domain.Entities.Abstracts;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Helpers;

namespace CashFlow.Transactions.Domain.Models;

public sealed class Transaction : Entity
{
    private Transaction()
    {
    }

    public Guid AccountId { get; private set; }

    public TransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public string? Description { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string RequestFingerprint { get; private set; } = string.Empty;

    public decimal BalanceAfter { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public decimal SignedAmount => Type == TransactionType.Credit ? Amount : -Amount;

    internal static Transaction Create(
        Guid id,
        Guid accountId,
        TransactionType type,
        decimal amount,
        string? description,
        string idempotencyKey,
        string requestFingerprint,
        decimal balanceAfter,
        DateTimeOffset occurredAt) => new()
        {
            Id = id,
            AccountId = accountId,
            Type = type,
            Amount = amount,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            BalanceAfter = balanceAfter,
            OccurredAt = occurredAt,
        };
}
