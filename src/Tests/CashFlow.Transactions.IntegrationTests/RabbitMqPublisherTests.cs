using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CashFlow.Transactions.IntegrationTests;

public sealed class RabbitMqPublisherTests
{
    private readonly TestInfrastructure _infra;

    public RabbitMqPublisherTests(TestInfrastructure infra)
    {
        _infra = infra;
    }

    private RabbitMqOptions Options(string exchange)
    {
        var user = _infra.RabbitMqUri.UserInfo.Split(':');
        return new RabbitMqOptions
        {
            HostName = _infra.RabbitMqUri.Host,
            Port = _infra.RabbitMqUri.Port,
            UserName = Uri.UnescapeDataString(user[0]),
            Password = Uri.UnescapeDataString(user[1]),
            Exchange = exchange,
        };
    }

    [Fact]
    public async Task Published_message_reaches_bound_queue_with_amqp_metadata()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options("test.ex." + Guid.NewGuid().ToString("N")));
        await using var connection = new RabbitMqConnection(options);
        await using var publisher = new RabbitMqPublisher(connection, options);

        var conn = await connection.GetAsync(CancellationToken.None);
        await using var channel = await conn.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(options.Value.Exchange, ExchangeType.Topic, durable: true);
        var queue = (await channel.QueueDeclareAsync(exclusive: true)).QueueName;
        await channel.QueueBindAsync(queue, options.Value.Exchange, "transaction.registered.v1");

        var messageId = Guid.NewGuid();
        await publisher.PublishAsync(
            new OutgoingMessage(messageId, "transaction.registered.v1", """{"x":1}""", DateTimeOffset.UtcNow, "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"),
            CancellationToken.None);

        BasicGetResult? received = null;
        for (var i = 0; i < 50 && received is null; i++)
        {
            received = await channel.BasicGetAsync(queue, autoAck: true);
            if (received is null)
            {
                await Task.Delay(100);
            }
        }

        received.ShouldNotBeNull();
        received.BasicProperties.MessageId.ShouldBe(messageId.ToString());
        received.BasicProperties.Type.ShouldBe("transaction.registered.v1");
        received.BasicProperties.DeliveryMode.ShouldBe(DeliveryModes.Persistent);
        received.BasicProperties.Headers!.ShouldContainKey("traceparent");
    }

    [Fact]
    public async Task Unroutable_message_is_reported_as_failure_so_outbox_retries()
    {
        var options = Microsoft.Extensions.Options.Options.Create(Options("test.unrouted." + Guid.NewGuid().ToString("N")));
        await using var connection = new RabbitMqConnection(options);
        await using var publisher = new RabbitMqPublisher(connection, options);

        await Should.ThrowAsync<Exception>(() => publisher.PublishAsync(
            new OutgoingMessage(Guid.NewGuid(), "transaction.registered.v1", "{}", DateTimeOffset.UtcNow, null),
            CancellationToken.None));
    }
}
