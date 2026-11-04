using System.Text.Json;
using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Infrastructure.Outbox;

namespace CashFlow.Transactions.UnitTests.Contracts;

public sealed class ProducerContractTests
{
    private static JsonElement Example(string name) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "Examples", name))).RootElement;

    private static HashSet<string> Fields(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void TransactionRegisteredV1_matches_published_contract()
    {
        var evt = new TransactionRegisteredV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "credit", 150.75m, "BRL", DateTimeOffset.UtcNow);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(evt, IntegrationEventJson.Options)).RootElement;
        var example = Example("transaction.registered.v1.json");

        Fields(json).SetEquals(Fields(example)).ShouldBeTrue();
        json.GetProperty("amount").ValueKind.ShouldBe(JsonValueKind.Number);
        TransactionRegisteredV1.EventType.ShouldBe("transaction.registered.v1");
    }

    [Fact]
    public void AccountOpenedV1_matches_published_contract()
    {
        var evt = new AccountOpenedV1(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(evt, IntegrationEventJson.Options)).RootElement;

        Fields(json).SetEquals(Fields(Example("account.opened.v1.json"))).ShouldBeTrue();
        AccountOpenedV1.EventType.ShouldBe("account.opened.v1");
    }
}
