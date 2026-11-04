using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Domain.Helpers;
using CashFlow.Transactions.Domain.Interfaces.Service;

namespace CashFlow.Transactions.Application.Transactions.Queries.ListTransactions;

public sealed class ListTransactionsQueryHandler : IQueryHandler<ListTransactionsQuery, IReadOnlyList<TransactionResponse>>
{
    private readonly ITransactionService _service;

    public ListTransactionsQueryHandler(ITransactionService service)
    {
        _service = service;
    }

    public async Task<Result<IReadOnlyList<TransactionResponse>>> HandleAsync(
        ListTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.ListByAccountAsync(
            query.AccountId,
            query.From,
            query.To,
            query.Limit,
            cancellationToken);

        return result.IsFailure
            ? result.Error!
            : Result.Success<IReadOnlyList<TransactionResponse>>(result.Value.Select(TransactionResponse.From).ToList());
    }
}
