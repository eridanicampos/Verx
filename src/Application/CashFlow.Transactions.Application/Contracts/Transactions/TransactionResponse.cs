using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Application.Contracts.Transactions;

public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string Type,
    decimal Amount,
    string? Description,
    decimal BalanceAfter,
    DateTimeOffset OccurredAt)
{
    public static TransactionResponse From(Transaction t) => new(
        t.Id,
        t.AccountId,
        t.Type.ToString().ToLowerInvariant(),
        t.Amount,
        t.Description,
        t.BalanceAfter,
        t.OccurredAt);
}

public sealed record RegisterTransactionResponse(TransactionResponse Transaction, bool Replayed);
