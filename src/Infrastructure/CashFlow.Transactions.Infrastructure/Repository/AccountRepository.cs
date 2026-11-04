using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;
using CashFlow.Transactions.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.Infrastructure.Repository;

internal sealed class AccountRepository : IAccountRepository
{
    private readonly TransactionsDbContext _db;

    public AccountRepository(TransactionsDbContext db)
    {
        _db = db;
    }

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> ExistsByDocumentHashAsync(string documentHash, CancellationToken cancellationToken) =>
        _db.Accounts.AnyAsync(a => a.HolderDocumentHash == documentHash, cancellationToken);

    public void Add(Account account) => _db.Accounts.Add(account);
}
