using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Domain.Interfaces.Repository;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByDocumentHashAsync(string documentHash, CancellationToken cancellationToken);

    void Add(Account account);
}
