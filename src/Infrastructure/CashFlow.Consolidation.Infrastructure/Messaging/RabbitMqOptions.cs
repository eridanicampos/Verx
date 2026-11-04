namespace CashFlow.Consolidation.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "cashflow";

    public string Password { get; set; } = string.Empty;

    public bool UseTls { get; set; }

    public string SourceExchange { get; set; } = "cashflow.transactions";

    public string Queue { get; set; } = "consolidation.transactions";

    public ushort PrefetchCount { get; set; } = 32;

    public int MaxRetries { get; set; } = 5;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);

    public bool ConsumerEnabled { get; set; } = true;

    public string RetryQueue => $"{Queue}.retry";

    public string DeadLetterExchange => $"{Queue}.dlx";

    public string DeadLetterQueue => $"{Queue}.dlq";
}
