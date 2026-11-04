using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Service;

namespace CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;

public sealed class RegisterTransactionCommandHandler : ICommandHandler<RegisterTransactionCommand, RegisterTransactionResponse>
{
    private readonly ITransactionService _service;

    public RegisterTransactionCommandHandler(ITransactionService service)
    {
        _service = service;
    }

    public async Task<Result<RegisterTransactionResponse>> HandleAsync(
        RegisterTransactionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _service.RegisterAsync(
            command.AccountId,
            command.Type,
            command.Amount,
            command.Description,
            command.IdempotencyKey,
            command.Fingerprint(),
            cancellationToken);

        return result.IsFailure
            ? result.Error!
            : new RegisterTransactionResponse(TransactionResponse.From(result.Value.Transaction), result.Value.Replayed);
    }
}
