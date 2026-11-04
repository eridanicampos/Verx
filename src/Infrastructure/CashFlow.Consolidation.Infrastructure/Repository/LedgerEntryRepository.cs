using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.Repository;

internal sealed class LedgerEntryRepository : ILedgerEntryRepository
{
    private readonly ConsolidationDbContext _db;

    public LedgerEntryRepository(ConsolidationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> TryAddAsync(LedgerEntry entry, CancellationToken cancellationToken)
    {
        var rows = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO consolidation.ledger_entries (transaction_id, account_id, signed_amount, occurred_at, business_date, projected_at)
            VALUES ({entry.TransactionId}, {entry.AccountId}, {entry.SignedAmount}, {entry.OccurredAt}, {entry.BusinessDate}, {entry.ProjectedAt})
            ON CONFLICT (transaction_id) DO NOTHING
            """,
            cancellationToken);
        return rows == 1;
    }
}
