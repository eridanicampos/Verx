using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Application.Balances.Queries.GetBalanceAt;
using CashFlow.Consolidation.Application.Balances.Queries.GetDailyBalances;
using CashFlow.Consolidation.Application.Balances.Services;
using CashFlow.Consolidation.Application.Contracts.Balances;
using CashFlow.Consolidation.Application.Projections;
using CashFlow.Consolidation.Domain.Interfaces.Service;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Consolidation.Application.Configuration;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IBalanceService, BalanceService>();
        services.AddScoped<TransactionRegisteredProjector>();
        services.AddScoped<AccountOpenedProjector>();
        services.AddScoped<IMessageDispatcher, MessageDispatcher>();
        services.AddScoped<IQueryHandler<GetBalanceAtQuery, BalanceResponse>, GetBalanceAtQueryHandler>();
        services.AddScoped<IQueryHandler<GetDailyBalancesQuery, IReadOnlyList<DailyBalanceResponse>>, GetDailyBalancesQueryHandler>();
        return services;
    }
}
