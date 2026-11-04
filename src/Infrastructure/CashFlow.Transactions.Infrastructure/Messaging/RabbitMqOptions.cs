namespace CashFlow.Transactions.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "cashflow";

    public string Password { get; set; } = string.Empty;

    public bool UseTls { get; set; }

    public string Exchange { get; set; } = "cashflow.transactions";

    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
