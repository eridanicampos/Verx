using CashFlow.Consolidation.Application.Messages;
using CashFlow.Consolidation.Application.Projections;

namespace CashFlow.Consolidation.UnitTests;

public sealed class ConsumerContractTests
{
    private static byte[] Example(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Contracts", "Examples", name));

    [Fact]
    public void Reads_transaction_registered_v1_example()
    {
        var message = MessageDispatcher.Deserialize<TransactionRegisteredMessage>(Example("transaction.registered.v1.json"));

        message.TransactionId.ShouldNotBe(Guid.Empty);
        message.AccountId.ShouldNotBe(Guid.Empty);
        message.Type.ShouldBe("credit");
        message.Amount.ShouldBe(150.75m);
        message.Currency.ShouldBe("BRL");
        TransactionRegisteredProjector.Validate(message).ShouldBe(CashFlow.Consolidation.Domain.Enums.EntryDirection.Credit);
    }

    [Fact]
    public void Reads_account_opened_v1_example()
    {
        var message = MessageDispatcher.Deserialize<AccountOpenedMessage>(Example("account.opened.v1.json"));

        message.AccountId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Tolerates_unknown_fields_added_by_producer()
    {
        var json = """{"eventId":"0192f3a1-7c2e-7b1a-9f00-1a2b3c4d5e6f","transactionId":"0192f3a1-7c2e-7b1a-9f00-aaaaaaaaaaaa","accountId":"0192f3a1-0000-7000-8000-bbbbbbbbbbbb","type":"debit","amount":1,"currency":"BRL","occurredAt":"2026-10-29T13:45:10Z","channel":"pix"}"""u8.ToArray();

        MessageDispatcher.Deserialize<TransactionRegisteredMessage>(json).Type.ShouldBe("debit");
    }
}
