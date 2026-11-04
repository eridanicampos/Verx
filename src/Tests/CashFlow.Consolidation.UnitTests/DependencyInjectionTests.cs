using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Balances.Queries.GetBalanceAt;
using CashFlow.Consolidation.Application.Balances.Queries.GetDailyBalances;
using CashFlow.Consolidation.Application.Configuration;
using CashFlow.Consolidation.Application.Contracts.Balances;
using CashFlow.Consolidation.Application.Projections;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Infrastructure.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CashFlow.Consolidation.UnitTests;

public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Consolidation"] = "Host=localhost;Database=unused",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    [Fact]
    public void Unit_of_work_exposes_repositories_sharing_the_same_instance()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        unitOfWork.BalanceReadRepository.ShouldNotBeNull();
        unitOfWork.InboxRepository.ShouldNotBeNull();
        unitOfWork.LedgerEntryRepository.ShouldNotBeNull();
        unitOfWork.DailyBalanceRepository.ShouldNotBeNull();
        unitOfWork.ConsolidatedAccountRepository.ShouldNotBeNull();
        unitOfWork.BalanceReadRepository.ShouldBeSameAs(unitOfWork.BalanceReadRepository);
    }

    [Fact]
    public void Handlers_and_dispatcher_are_resolvable()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        sp.GetRequiredService<IQueryHandler<GetBalanceAtQuery, BalanceResponse>>().ShouldNotBeNull();
        sp.GetRequiredService<IQueryHandler<GetDailyBalancesQuery, IReadOnlyList<DailyBalanceResponse>>>().ShouldNotBeNull();
        sp.GetRequiredService<IMessageDispatcher>().ShouldNotBeNull();
    }
}
