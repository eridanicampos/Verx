using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.Repository;

internal sealed class ConsolidatedAccountRepository : IConsolidatedAccountRepository
{
    private readonly ConsolidationDbContext _db;

    public ConsolidatedAccountRepository(ConsolidationDbContext db)
    {
        _db = db;
    }

    public Task UpsertAsync(Guid accountId, DateTimeOffset openedAt, DateTimeOffset? lastEntryAt, CancellationToken cancellationToken) =>
        _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO consolidation.accounts (account_id, opened_at, last_entry_at)
            VALUES ({accountId}, {openedAt}, {lastEntryAt})
            ON CONFLICT (account_id) DO UPDATE SET
                opened_at     = LEAST(accounts.opened_at, EXCLUDED.opened_at),
                last_entry_at = GREATEST(accounts.last_entry_at, EXCLUDED.last_entry_at)
            """,
            cancellationToken);
}
