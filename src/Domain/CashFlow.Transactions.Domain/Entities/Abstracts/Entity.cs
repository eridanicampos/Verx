namespace CashFlow.Transactions.Domain.Entities.Abstracts;

public abstract class Entity
{
    public Guid Id { get; protected init; }
}
