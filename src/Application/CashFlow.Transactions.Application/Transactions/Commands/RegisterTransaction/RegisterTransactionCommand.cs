using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Domain.Enums;

namespace CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;

public sealed record RegisterTransactionCommand(
    Guid AccountId,
    TransactionType Type,
    decimal Amount,
    string? Description,
    string IdempotencyKey)
    : ICommand<RegisterTransactionResponse>
{
    public string Fingerprint()
    {
        var canonical = string.Join(
            '|',
            AccountId.ToString("N"),
            ((int)Type).ToString(CultureInfo.InvariantCulture),
            Amount.ToString("0.00", CultureInfo.InvariantCulture),
            Description?.Trim() ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
