using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Contracts.Accounts;

namespace CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;

public sealed record OpenAccountCommand(string HolderName, string HolderDocument, decimal OverdraftLimit)
    : ICommand<AccountResponse>;
