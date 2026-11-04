using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Transactions;

namespace CashFlow.Transactions.Application.Transactions.Queries.ListTransactions;

public sealed record ListTransactionsQuery(Guid AccountId, DateTimeOffset? From, DateTimeOffset? To, int Limit = 100)
    : IQuery<IReadOnlyList<TransactionResponse>>;
