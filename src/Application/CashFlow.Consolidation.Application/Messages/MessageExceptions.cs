namespace CashFlow.Consolidation.Application.Messages;

public sealed class PermanentMessageException : Exception
{
    public PermanentMessageException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
