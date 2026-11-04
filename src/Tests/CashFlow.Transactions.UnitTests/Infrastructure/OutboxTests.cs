using CashFlow.Transactions.Infrastructure.Outbox;

namespace CashFlow.Transactions.UnitTests.Infrastructure;

public sealed class OutboxTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 8)]
    [InlineData(20, 300)]
    public void Backoff_is_exponential_and_capped(int attempt, int expectedSeconds)
    {
        OutboxProcessor.Backoff(attempt, TimeSpan.FromMinutes(5)).ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void Failed_message_is_rescheduled_and_error_truncated()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new OutboxMessage(Guid.NewGuid(), "t", "{}", now, null);

        message.MarkFailed(new string('x', 5000), now.AddSeconds(2));

        message.Attempts.ShouldBe(1);
        message.ProcessedAt.ShouldBeNull();
        message.NextAttemptAt.ShouldBe(now.AddSeconds(2));
        message.LastError!.Length.ShouldBe(2000);
    }

    [Fact]
    public void Published_message_is_marked_processed()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new OutboxMessage(Guid.NewGuid(), "t", "{}", now, null);
        message.MarkFailed("boom", now);

        message.MarkPublished(now.AddSeconds(5));

        message.ProcessedAt.ShouldBe(now.AddSeconds(5));
        message.LastError.ShouldBeNull();
        message.Attempts.ShouldBe(2);
    }
}
