using CashFlow.Transactions.Domain.Entities.Abstracts;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Repository;

namespace CashFlow.Transactions.Domain.Models;

public sealed class Account : Entity
{
    private Account()
    {
    }

    public string HolderName { get; private set; } = string.Empty;

    public string HolderDocument { get; private set; } = string.Empty;

    public string HolderDocumentHash { get; private set; } = string.Empty;

    public decimal Balance { get; private set; }

    public decimal OverdraftLimit { get; private set; }

    public AccountStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public long Version { get; private set; }

    public static Result<Account> Open(
        Guid id,
        string holderName,
        string holderDocument,
        string holderDocumentHash,
        decimal overdraftLimit,
        DateTimeOffset now)
    {
        if (overdraftLimit < 0)
        {
            return AccountErrors.InvalidOverdraft;
        }

        return new Account
        {
            Id = id,
            HolderName = holderName.Trim(),
            HolderDocument = holderDocument,
            HolderDocumentHash = holderDocumentHash,
            OverdraftLimit = overdraftLimit,
            Balance = 0m,
            Status = AccountStatus.Active,
            CreatedAt = now,
            Version = 0,
        };
    }

    public Result<Transaction> Register(
        Guid transactionId,
        TransactionType type,
        decimal amount,
        string? description,
        string idempotencyKey,
        string requestFingerprint,
        DateTimeOffset now)
    {
        if (Status != AccountStatus.Active)
        {
            return AccountErrors.Inactive;
        }

        if (amount <= 0)
        {
            return AccountErrors.InvalidAmount;
        }

        var signedAmount = type == TransactionType.Credit ? amount : -amount;
        var newBalance = Balance + signedAmount;

        if (type == TransactionType.Debit && newBalance < -OverdraftLimit)
        {
            return AccountErrors.InsufficientFunds;
        }

        Balance = newBalance;
        Version++;

        return Transaction.Create(
            transactionId,
            Id,
            type,
            amount,
            description,
            idempotencyKey,
            requestFingerprint,
            newBalance,
            now);
    }

    public void Block() => Status = AccountStatus.Blocked;
}
