using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.Repository;

internal sealed class DailyBalanceRepository : IDailyBalanceRepository
{
    private readonly ConsolidationDbContext _db;

    public DailyBalanceRepository(ConsolidationDbContext db)
    {
        _db = db;
    }

    public Task ApplyAsync(LedgerEntry entry, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var credit = entry.SignedAmount > 0 ? entry.SignedAmount : 0m;
        var debit = entry.SignedAmount < 0 ? -entry.SignedAmount : 0m;
        return _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO consolidation.daily_balances (account_id, date, total_credits, total_debits, entry_count, updated_at)
            VALUES ({entry.AccountId}, {entry.BusinessDate}, {credit}, {debit}, 1, {now})
            ON CONFLICT (account_id, date) DO UPDATE SET
                total_credits = daily_balances.total_credits + EXCLUDED.total_credits,
                total_debits  = daily_balances.total_debits  + EXCLUDED.total_debits,
                entry_count   = daily_balances.entry_count + 1,
                updated_at    = EXCLUDED.updated_at
            """,
            cancellationToken);
    }
}
