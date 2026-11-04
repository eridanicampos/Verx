using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Application.Contracts.Accounts;

public sealed record AccountResponse(
    Guid Id,
    string HolderName,
    string HolderDocumentMasked,
    decimal Balance,
    decimal OverdraftLimit,
    string Status,
    DateTimeOffset CreatedAt)
{
    public static AccountResponse From(Account account, string plainDocument) => new(
        account.Id,
        account.HolderName,
        Mask(plainDocument),
        account.Balance,
        account.OverdraftLimit,
        account.Status.ToString(),
        account.CreatedAt);

    internal static string Mask(string document)
    {
        var digits = new string(document.Where(char.IsDigit).ToArray());
        return digits.Length <= 2 ? "**" : new string('*', digits.Length - 2) + digits[^2..];
    }
}
