using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Contracts.Balances;

namespace CashFlow.Consolidation.Application.Balances.Queries.GetBalanceAt;

public sealed record GetBalanceAtQuery(Guid AccountId, DateTimeOffset? At) : IQuery<BalanceResponse>;
