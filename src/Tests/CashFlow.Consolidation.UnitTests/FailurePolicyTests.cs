using System.Text;
using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Infrastructure.Messaging;

namespace CashFlow.Consolidation.UnitTests;

public sealed class FailurePolicyTests
{
    [Fact]
    public void Permanent_failures_go_straight_to_dlq()
    {
        FailurePolicy.Decide(new PermanentMessageException("bad"), retryCount: 0, maxRetries: 5).ShouldBe(FailureAction.DeadLetter);
    }

    [Theory]
    [InlineData(0, FailureAction.Retry)]
    [InlineData(4, FailureAction.Retry)]
    [InlineData(5, FailureAction.DeadLetter)]
    [InlineData(9, FailureAction.DeadLetter)]
    public void Transient_failures_are_retried_until_limit(int retryCount, FailureAction expected)
    {
        FailurePolicy.Decide(new TimeoutException(), retryCount, maxRetries: 5).ShouldBe(expected);
    }

    [Fact]
    public void Retry_count_is_read_from_any_amqp_encoding()
    {
        FailurePolicy.ReadRetryCount(null).ShouldBe(0);
        FailurePolicy.ReadRetryCount(new Dictionary<string, object?>()).ShouldBe(0);
        FailurePolicy.ReadRetryCount(new Dictionary<string, object?> { [FailurePolicy.RetryCountHeader] = 3 }).ShouldBe(3);
        FailurePolicy.ReadRetryCount(new Dictionary<string, object?> { [FailurePolicy.RetryCountHeader] = 4L }).ShouldBe(4);
        FailurePolicy.ReadRetryCount(new Dictionary<string, object?> { [FailurePolicy.RetryCountHeader] = Encoding.UTF8.GetBytes("2") }).ShouldBe(2);
    }
}
