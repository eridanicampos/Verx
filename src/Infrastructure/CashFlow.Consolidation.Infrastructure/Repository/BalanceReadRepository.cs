using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using CashFlow.Consolidation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.Repository;

internal sealed class BalanceReadRepository : IBalanceReadRepository
{
    private readonly ConsolidationDbContext _db;

    public BalanceReadRepository(ConsolidationDbContext db)
    {
        _db = db;
    }

    public Task<ConsolidatedAccount?> GetAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken);

    public async Task<BalanceAggregate> SumDailyBeforeAsync(Guid accountId, DateOnly date, CancellationToken cancellationToken)
    {
        var sums = await _db.DailyBalances.AsNoTracking()
            .Where(d => d.AccountId == accountId && d.Date < date)
            .GroupBy(_ => 1)
            .Select(g => new { Credits = g.Sum(d => d.TotalCredits), Debits = g.Sum(d => d.TotalDebits) })
            .FirstOrDefaultAsync(cancellationToken);
        return sums is null ? new BalanceAggregate(0, 0) : new BalanceAggregate(sums.Credits, sums.Debits);
    }

    public async Task<BalanceAggregate> SumEntriesOfDayUntilAsync(Guid accountId, DateOnly date, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var sums = await _db.LedgerEntries.AsNoTracking()
            .Where(e => e.AccountId == accountId && e.BusinessDate == date && e.OccurredAt <= until)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Credits = g.Sum(e => e.SignedAmount > 0 ? e.SignedAmount : 0m),
                Debits = g.Sum(e => e.SignedAmount < 0 ? -e.SignedAmount : 0m),
            })
            .FirstOrDefaultAsync(cancellationToken);
        return sums is null ? new BalanceAggregate(0, 0) : new BalanceAggregate(sums.Credits, sums.Debits);
    }

    public async Task<IReadOnlyList<DailyBalance>> ListDailyAsync(Guid accountId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await _db.DailyBalances.AsNoTracking()
            .Where(d => d.AccountId == accountId && d.Date >= from && d.Date <= to)
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken);
}
