using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;
using CashFlow.Transactions.Application.Accounts.Queries.GetAccountById;
using CashFlow.Transactions.Application.Configuration;
using CashFlow.Transactions.Application.Contracts.Accounts;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;
using CashFlow.Transactions.Application.Transactions.Queries.ListTransactions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Infrastructure.Configurations;
using CashFlow.Transactions.UnitTests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CashFlow.Transactions.UnitTests.Configuration;

public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Transactions"] = "Host=localhost;Database=unused",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddSingleton<IPiiProtector, FakePii>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void Unit_of_work_exposes_repositories_sharing_the_same_instance()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        unitOfWork.AccountRepository.ShouldNotBeNull();
        unitOfWork.TransactionRepository.ShouldNotBeNull();
        unitOfWork.AccountRepository.ShouldBeSameAs(unitOfWork.AccountRepository);
    }

    [Fact]
    public void Handlers_are_resolvable()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<ICommandHandler<OpenAccountCommand, AccountResponse>>().ShouldNotBeNull();
        sp.GetRequiredService<ICommandHandler<RegisterTransactionCommand, RegisterTransactionResponse>>().ShouldNotBeNull();
        sp.GetRequiredService<IQueryHandler<GetAccountByIdQuery, AccountResponse>>().ShouldNotBeNull();
        sp.GetRequiredService<IQueryHandler<ListTransactionsQuery, IReadOnlyList<TransactionResponse>>>().ShouldNotBeNull();
    }
}
