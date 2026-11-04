using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Contracts.Balances;
using CashFlow.Consolidation.Domain.Helpers;
using CashFlow.Consolidation.Domain.Interfaces.Service;

namespace CashFlow.Consolidation.Application.Balances.Queries.GetBalanceAt;

public sealed class GetBalanceAtQueryHandler : IQueryHandler<GetBalanceAtQuery, BalanceResponse>
{
    private readonly IBalanceService _service;

    public GetBalanceAtQueryHandler(IBalanceService service)
    {
        _service = service;
    }

    public async Task<Result<BalanceResponse>> HandleAsync(GetBalanceAtQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetBalanceAtAsync(query.AccountId, query.At, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        var balance = result.Value;
        return new BalanceResponse(balance.AccountId, balance.At, balance.Balance, balance.TotalCredits, balance.TotalDebits, balance.LastEntryAt);
    }
}
