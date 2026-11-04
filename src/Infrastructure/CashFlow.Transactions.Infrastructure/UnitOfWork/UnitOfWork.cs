using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CashFlow.Transactions.Infrastructure.UnitOfWork;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly TransactionsDbContext _db;

    private IAccountRepository? _accountRepository;
    private ITransactionRepository? _transactionRepository;

    public UnitOfWork(TransactionsDbContext db)
    {
        _db = db;
    }

    public IAccountRepository AccountRepository => _accountRepository ??= new AccountRepository(_db);

    public ITransactionRepository TransactionRepository => _transactionRepository ??= new TransactionRepository(_db);

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Registro alterado por outra transação.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintException("Violação de unicidade.", ex);
        }
    }

    public void Reset() => _db.ChangeTracker.Clear();
}
