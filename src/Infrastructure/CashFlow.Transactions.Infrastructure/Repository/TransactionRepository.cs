using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;
using CashFlow.Transactions.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.Infrastructure.Repository;

internal sealed class TransactionRepository : ITransactionRepository
{
    private readonly TransactionsDbContext _db;

    public TransactionRepository(TransactionsDbContext db)
    {
        _db = db;
    }

    public Task<Transaction?> GetByIdempotencyKeyAsync(Guid accountId, string idempotencyKey, CancellationToken cancellationToken) =>
        _db.Transactions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.AccountId == accountId && t.IdempotencyKey == idempotencyKey, cancellationToken);

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> ListByAccountAsync(
        Guid accountId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = _db.Transactions.AsNoTracking().Where(t => t.AccountId == accountId);
        if (from is not null)
        {
            query = query.Where(t => t.OccurredAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(t => t.OccurredAt <= to);
        }

        return await query.OrderByDescending(t => t.OccurredAt).Take(limit).ToListAsync(cancellationToken);
    }

    public void Add(Transaction transaction) => _db.Transactions.Add(transaction);
}
