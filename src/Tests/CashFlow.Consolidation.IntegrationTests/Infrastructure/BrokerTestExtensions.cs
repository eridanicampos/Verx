using System.Text;
using System.Text.Json;
using CashFlow.Consolidation.Infrastructure.Messaging;
using RabbitMQ.Client;

namespace CashFlow.Consolidation.IntegrationTests.Infrastructure;

public static class BrokerTestExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task PublishAsync(this ConsolidationApiFactory factory, string type, object payload, Guid? messageId = null) =>
        await factory.PublishRawAsync(type, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Json)), messageId);

    public static async Task PublishRawAsync(this ConsolidationApiFactory factory, string type, byte[] body, Guid? messageId = null)
    {
        var conn = await factory.Broker.GetAsync(CancellationToken.None);
        await using var channel = await conn.CreateChannelAsync(new CreateChannelOptions(true, true));
        var props = new BasicProperties
        {
            MessageId = (messageId ?? Guid.NewGuid()).ToString(),
            Type = type,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };
        await channel.BasicPublishAsync(factory.Exchange, type, mandatory: true, props, body);
    }

    public static async Task<BasicGetResult?> WaitForDeadLetterAsync(this ConsolidationApiFactory factory, TimeSpan timeout)
    {
        var conn = await factory.Broker.GetAsync(CancellationToken.None);
        await using var channel = await conn.CreateChannelAsync();
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(factory.Queue + ".dlq", autoAck: true);
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(100);
        }

        return null;
    }

    public static int RetryCount(this BasicGetResult message) =>
        FailurePolicy.ReadRetryCount(message.BasicProperties.Headers);

    public static async Task<T> EventuallyAsync<T>(Func<Task<T>> probe, Func<T, bool> until, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        T last;
        do
        {
            last = await probe();
            if (until(last))
            {
                return last;
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        return last;
    }
}
