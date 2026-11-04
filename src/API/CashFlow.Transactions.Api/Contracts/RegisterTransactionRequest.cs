namespace CashFlow.Transactions.Api.Contracts;

public sealed record RegisterTransactionRequest(string Type, decimal Amount, string? Description);
