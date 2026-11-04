using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Models;

namespace CashFlow.Transactions.Domain.Interfaces.Service;

public interface IAccountService
{
    Task<Result<AccountDetails>> OpenAsync(string holderName, string holderDocument, decimal overdraftLimit, CancellationToken cancellationToken);

    Task<Result<AccountDetails>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
