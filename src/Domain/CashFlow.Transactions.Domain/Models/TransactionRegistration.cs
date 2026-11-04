namespace CashFlow.Transactions.Domain.Models;

public sealed record TransactionRegistration(Transaction Transaction, bool Replayed);
