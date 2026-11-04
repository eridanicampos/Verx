namespace CashFlow.Transactions.Infrastructure.Security;

public sealed class PiiOptions
{
    public const string SectionName = "Pii";

    public string ActiveKeyId { get; set; } = "k1";

    public Dictionary<string, string> EncryptionKeys { get; set; } = new(StringComparer.Ordinal);

    public string BlindIndexKey { get; set; } = string.Empty;
}
