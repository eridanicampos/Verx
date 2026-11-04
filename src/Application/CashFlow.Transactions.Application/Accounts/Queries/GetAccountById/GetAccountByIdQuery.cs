using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Accounts;

namespace CashFlow.Transactions.Application.Accounts.Queries.GetAccountById;

public sealed record GetAccountByIdQuery(Guid AccountId) : IQuery<AccountResponse>;
