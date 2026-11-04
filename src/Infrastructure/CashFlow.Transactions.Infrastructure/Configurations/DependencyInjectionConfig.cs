using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;
using CashFlow.Transactions.Infrastructure.Context;
using CashFlow.Transactions.Infrastructure.Health;
using CashFlow.Transactions.Infrastructure.Messaging;
using CashFlow.Transactions.Infrastructure.Outbox;
using CashFlow.Transactions.Infrastructure.Repository;
using CashFlow.Transactions.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.Infrastructure.Configurations;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Transactions")
            ?? throw new InvalidOperationException("ConnectionStrings:Transactions não configurada.");

        services.AddDbContext<TransactionsDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "transactions");
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorCodesToAdd: null);
            }));

        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();
        services.AddScoped<IOutbox, EfOutbox>();

        services.AddOptions<PiiOptions>().Bind(configuration.GetSection(PiiOptions.SectionName));
        services.AddSingleton<IPiiProtector, AesGcmPiiProtector>();

        services.AddOptions<RabbitMqOptions>().Bind(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddOptions<OutboxOptions>().Bind(configuration.GetSection(OutboxOptions.SectionName));
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
        services.AddSingleton<OutboxProcessor>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxProcessor>());

        services.AddHealthChecks()
            .AddDbContextCheck<TransactionsDbContext>("postgres", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"])
            .AddCheck<OutboxBacklogHealthCheck>("outbox", tags: ["ready"]);

        return services;
    }
}
