using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Accounts;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Service;

namespace CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;

public sealed class OpenAccountCommandHandler : ICommandHandler<OpenAccountCommand, AccountResponse>
{
    private readonly IAccountService _service;

    public OpenAccountCommandHandler(IAccountService service)
    {
        _service = service;
    }

    public async Task<Result<AccountResponse>> HandleAsync(OpenAccountCommand command, CancellationToken cancellationToken)
    {
        var result = await _service.OpenAsync(
            command.HolderName,
            command.HolderDocument,
            command.OverdraftLimit,
            cancellationToken);

        return result.IsFailure
            ? result.Error!
            : AccountResponse.From(result.Value.Account, result.Value.HolderDocument);
    }
}
