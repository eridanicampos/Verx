namespace CashFlow.Consolidation.Domain.Models;

public sealed record DailyBalanceStatement(DailyBalance Day, decimal ClosingBalance);
