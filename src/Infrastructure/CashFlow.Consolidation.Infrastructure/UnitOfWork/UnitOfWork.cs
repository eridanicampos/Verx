using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Infrastructure.Context;
using CashFlow.Consolidation.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.UnitOfWork;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ConsolidationDbContext _db;

    private IBalanceReadRepository? _balanceReadRepository;
    private IInboxRepository? _inboxRepository;
    private ILedgerEntryRepository? _ledgerEntryRepository;
    private IDailyBalanceRepository? _dailyBalanceRepository;
    private IConsolidatedAccountRepository? _consolidatedAccountRepository;

    public UnitOfWork(ConsolidationDbContext db)
    {
        _db = db;
    }

    public IBalanceReadRepository BalanceReadRepository => _balanceReadRepository ??= new BalanceReadRepository(_db);

    public IInboxRepository InboxRepository => _inboxRepository ??= new InboxRepository(_db);

    public ILedgerEntryRepository LedgerEntryRepository => _ledgerEntryRepository ??= new LedgerEntryRepository(_db);

    public IDailyBalanceRepository DailyBalanceRepository => _dailyBalanceRepository ??= new DailyBalanceRepository(_db);

    public IConsolidatedAccountRepository ConsolidatedAccountRepository =>
        _consolidatedAccountRepository ??= new ConsolidatedAccountRepository(_db);

    public Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        // Com EnableRetryOnFailure a transação precisa rodar dentro da execution strategy,
        // para que uma falha transitória repita o bloco inteiro.
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(
            (Db: _db, Work: work),
            static async (_, state, ct) =>
            {
                await using var transaction = await state.Db.Database.BeginTransactionAsync(ct);
                var result = await state.Work(ct);
                await transaction.CommitAsync(ct);
                return result;
            },
            verifySucceeded: null,
            cancellationToken);
    }
}
