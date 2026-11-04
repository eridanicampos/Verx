namespace CashFlow.Transactions.Api.Contracts;

public sealed record OpenAccountRequest(string HolderName, string HolderDocument, decimal OverdraftLimit = 0);
