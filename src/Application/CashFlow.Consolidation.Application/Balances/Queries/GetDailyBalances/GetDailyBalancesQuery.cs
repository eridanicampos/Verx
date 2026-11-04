using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Contracts.Balances;

namespace CashFlow.Consolidation.Application.Balances.Queries.GetDailyBalances;

public sealed record GetDailyBalancesQuery(Guid AccountId, DateOnly From, DateOnly To) : IQuery<IReadOnlyList<DailyBalanceResponse>>;
