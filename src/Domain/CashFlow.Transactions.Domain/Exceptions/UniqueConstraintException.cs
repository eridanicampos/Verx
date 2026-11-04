namespace CashFlow.Transactions.Domain.Exceptions;

public sealed class UniqueConstraintException : Exception
{
    public UniqueConstraintException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
