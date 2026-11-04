using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.UnitTests.Domain;

public sealed class AccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static Account NewAccount(decimal overdraft = 0) =>
        Account.Open(Guid.NewGuid(), "Maria", "enc", "hash", overdraft, Now).Value;

    [Fact]
    public void Credit_increases_balance_and_version()
    {
        var account = NewAccount();

        var result = account.Register(Guid.NewGuid(), TransactionType.Credit, 100m, "dep", "k1", "fp", Now);

        result.IsSuccess.ShouldBeTrue();
        account.Balance.ShouldBe(100m);
        account.Version.ShouldBe(1);
        result.Value.BalanceAfter.ShouldBe(100m);
        result.Value.SignedAmount.ShouldBe(100m);
    }

    [Fact]
    public void Debit_within_overdraft_is_allowed()
    {
        var account = NewAccount(overdraft: 50);

        var result = account.Register(Guid.NewGuid(), TransactionType.Debit, 50m, null, "k1", "fp", Now);

        result.IsSuccess.ShouldBeTrue();
        account.Balance.ShouldBe(-50m);
        result.Value.SignedAmount.ShouldBe(-50m);
    }

    [Fact]
    public void Debit_beyond_overdraft_is_rejected_without_side_effects()
    {
        var account = NewAccount(overdraft: 50);

        var result = account.Register(Guid.NewGuid(), TransactionType.Debit, 50.01m, null, "k1", "fp", Now);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AccountErrors.InsufficientFunds);
        account.Balance.ShouldBe(0m);
        account.Version.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Non_positive_amount_is_rejected(decimal amount)
    {
        var result = NewAccount().Register(Guid.NewGuid(), TransactionType.Credit, amount, null, "k", "fp", Now);

        result.Error.ShouldBe(AccountErrors.InvalidAmount);
    }

    [Fact]
    public void Blocked_account_rejects_transactions()
    {
        var account = NewAccount();
        account.Block();

        account.Register(Guid.NewGuid(), TransactionType.Credit, 1m, null, "k", "fp", Now).Error.ShouldBe(AccountErrors.Inactive);
    }

    [Fact]
    public void Negative_overdraft_is_invalid()
    {
        Account.Open(Guid.NewGuid(), "x", "e", "h", -1, Now).Error.ShouldBe(AccountErrors.InvalidOverdraft);
    }

    [Fact]
    public void Blank_description_is_normalized_to_null()
    {
        var tx = NewAccount().Register(Guid.NewGuid(), TransactionType.Credit, 1m, "   ", "k", "fp", Now).Value;

        tx.Description.ShouldBeNull();
    }
}
