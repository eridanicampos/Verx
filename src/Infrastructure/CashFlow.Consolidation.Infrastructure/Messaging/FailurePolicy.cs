using CashFlow.Consolidation.Application.Messages;

namespace CashFlow.Consolidation.Infrastructure.Messaging;

public enum FailureAction
{
    Retry,
    DeadLetter,
}

public static class FailurePolicy
{
    public const string RetryCountHeader = "x-retry-count";

    public static FailureAction Decide(Exception exception, int retryCount, int maxRetries) =>
        exception is PermanentMessageException || retryCount >= maxRetries
            ? FailureAction.DeadLetter
            : FailureAction.Retry;

    public static int ReadRetryCount(IDictionary<string, object?>? headers) =>
        headers is not null && headers.TryGetValue(RetryCountHeader, out var value)
            ? value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes when int.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
                _ => 0,
            }
            : 0;
}
