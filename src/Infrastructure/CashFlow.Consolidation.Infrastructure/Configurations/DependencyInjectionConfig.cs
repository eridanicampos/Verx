using CashFlow.Consolidation.Application.Abstractions.Messaging;
using CashFlow.Consolidation.Domain.Interfaces.Common;
using CashFlow.Consolidation.Domain.Interfaces.Repository;
using CashFlow.Consolidation.Infrastructure.Context;
using CashFlow.Consolidation.Infrastructure.Health;
using CashFlow.Consolidation.Infrastructure.Messaging;
using CashFlow.Consolidation.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Consolidation.Infrastructure.Configurations;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Consolidation")
            ?? throw new InvalidOperationException("ConnectionStrings:Consolidation não configurada.");

        services.AddDbContext<ConsolidationDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", ConsolidationDbContext.Schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        services.AddOptions<RabbitMqOptions>().Bind(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<TransactionEventsConsumer>();
        services.AddHostedService(sp => sp.GetRequiredService<TransactionEventsConsumer>());

        services.AddHealthChecks()
            .AddDbContextCheck<ConsolidationDbContext>("postgres", tags: ["ready"])
            .AddCheck<ConsumerHealthCheck>("consumer", tags: ["ready"]);

        return services;
    }
}
