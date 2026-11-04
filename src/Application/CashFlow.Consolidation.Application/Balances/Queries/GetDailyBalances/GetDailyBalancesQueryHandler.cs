using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Contracts.Balances;
using CashFlow.Consolidation.Domain.Helpers;
using CashFlow.Consolidation.Domain.Interfaces.Service;

namespace CashFlow.Consolidation.Application.Balances.Queries.GetDailyBalances;

public sealed class GetDailyBalancesQueryHandler : IQueryHandler<GetDailyBalancesQuery, IReadOnlyList<DailyBalanceResponse>>
{
    private readonly IBalanceService _service;

    public GetDailyBalancesQueryHandler(IBalanceService service)
    {
        _service = service;
    }

    public async Task<Result<IReadOnlyList<DailyBalanceResponse>>> HandleAsync(
        GetDailyBalancesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetDailyBalancesAsync(query.AccountId, query.From, query.To, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        return result.Value
            .Select(s => new DailyBalanceResponse(s.Day.Date, s.Day.TotalCredits, s.Day.TotalDebits, s.Day.Net, s.ClosingBalance, s.Day.EntryCount))
            .ToList();
    }
}
