using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;
using CashFlow.Transactions.Application.Accounts.Services;
using CashFlow.Transactions.Application.Behaviors;
using CashFlow.Transactions.Application.Contracts.Accounts;
using CashFlow.Transactions.Application.IntegrationEvents;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.UnitTests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace CashFlow.Transactions.UnitTests.Application;

public sealed class OpenAccountCommandHandlerTests
{
    private readonly InMemoryStore _store = new();

    private ICommandHandler<OpenAccountCommand, AccountResponse> CreateHandler() =>
        new ValidationBehavior<OpenAccountCommand, AccountResponse>(
            new OpenAccountCommandHandler(new AccountService(_store, _store, new FakePii(), new FakeTimeProvider(DateTimeOffset.UtcNow))),
            [new OpenAccountCommandValidator()]);

    [Fact]
    public async Task Opens_account_with_encrypted_document_and_masked_response()
    {
        var result = await CreateHandler().HandleAsync(new OpenAccountCommand("Maria", "123.456.789-09", 100), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.HolderDocumentMasked.ShouldBe("*********09");
        var account = _store.Accounts.Values.ShouldHaveSingleItem();
        account.HolderDocument.ShouldBe("enc:12345678909");
        account.HolderDocumentHash.ShouldBe("h:12345678909");
        _store.PublishedToOutbox.ShouldHaveSingleItem().ShouldBeOfType<AccountOpenedV1>().AccountId.ShouldBe(account.Id);
    }

    [Fact]
    public async Task Duplicate_document_is_conflict()
    {
        var handler = CreateHandler();
        await handler.HandleAsync(new OpenAccountCommand("Maria", "12345678909", 0), CancellationToken.None);

        var result = await handler.HandleAsync(new OpenAccountCommand("Outra", "123.456.789-09", 0), CancellationToken.None);

        result.Error.ShouldBe(AccountErrors.DuplicateHolder);
    }

    [Fact]
    public async Task Unique_violation_on_save_is_conflict()
    {
        _store.FailuresOnSave.Enqueue(new UniqueConstraintException("dup"));

        var result = await CreateHandler().HandleAsync(new OpenAccountCommand("Maria", "12345678909", 0), CancellationToken.None);

        result.Error.ShouldBe(AccountErrors.DuplicateHolder);
    }

    [Theory]
    [InlineData("", "12345678909")]
    [InlineData("Maria", "123")]
    public async Task Invalid_input_fails_validation(string name, string document)
    {
        var result = await CreateHandler().HandleAsync(new OpenAccountCommand(name, document, 0), CancellationToken.None);

        result.Error!.Code.ShouldBe("validation.failed");
    }
}
