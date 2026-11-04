using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Accounts;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Service;

namespace CashFlow.Transactions.Application.Accounts.Queries.GetAccountById;

public sealed class GetAccountByIdQueryHandler : IQueryHandler<GetAccountByIdQuery, AccountResponse>
{
    private readonly IAccountService _service;

    public GetAccountByIdQueryHandler(IAccountService service)
    {
        _service = service;
    }

    public async Task<Result<AccountResponse>> HandleAsync(GetAccountByIdQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(query.AccountId, cancellationToken);

        return result.IsFailure
            ? result.Error!
            : AccountResponse.From(result.Value.Account, result.Value.HolderDocument);
    }
}
