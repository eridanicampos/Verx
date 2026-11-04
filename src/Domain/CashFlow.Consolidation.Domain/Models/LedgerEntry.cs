using CashFlow.Consolidation.Domain.Enums;
using CashFlow.Consolidation.Domain.Utils;

namespace CashFlow.Consolidation.Domain.Models;

public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    public Guid TransactionId { get; private set; }

    public Guid AccountId { get; private set; }

    public decimal SignedAmount { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateOnly BusinessDate { get; private set; }

    public DateTimeOffset ProjectedAt { get; private set; }

    public static LedgerEntry Create(
        Guid transactionId,
        Guid accountId,
        EntryDirection direction,
        decimal amount,
        DateTimeOffset occurredAt,
        DateTimeOffset projectedAt)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Valor deve ser positivo.");
        }

        return new LedgerEntry
        {
            TransactionId = transactionId,
            AccountId = accountId,
            SignedAmount = direction == EntryDirection.Credit ? amount : -amount,
            OccurredAt = occurredAt.ToUniversalTime(),
            BusinessDate = BusinessCalendar.DateOf(occurredAt),
            ProjectedAt = projectedAt,
        };
    }
}
