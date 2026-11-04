using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Accounts.Commands.OpenAccount;
using CashFlow.Transactions.Application.Accounts.Queries.GetAccountById;
using CashFlow.Transactions.Application.Accounts.Services;
using CashFlow.Transactions.Application.Behaviors;
using CashFlow.Transactions.Application.Contracts.Accounts;
using CashFlow.Transactions.Application.Contracts.Transactions;
using CashFlow.Transactions.Application.Transactions.Commands.RegisterTransaction;
using CashFlow.Transactions.Application.Transactions.Queries.ListTransactions;
using CashFlow.Transactions.Application.Transactions.Services;
using CashFlow.Transactions.Domain.Interfaces.Service;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlow.Transactions.Application.Configuration;

public static class DependencyInjectionConfig
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterTransactionCommandValidator>(ServiceLifetime.Singleton);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();

        services.AddCommandHandler<OpenAccountCommand, AccountResponse, OpenAccountCommandHandler>();
        services.AddCommandHandler<RegisterTransactionCommand, RegisterTransactionResponse, RegisterTransactionCommandHandler>();

        services.AddScoped<IQueryHandler<GetAccountByIdQuery, AccountResponse>, GetAccountByIdQueryHandler>();
        services.AddScoped<IQueryHandler<ListTransactionsQuery, IReadOnlyList<TransactionResponse>>, ListTransactionsQueryHandler>();

        return services;
    }

    private static void AddCommandHandler<TCommand, TResponse, THandler>(this IServiceCollection services)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResponse>>(sp => new ValidationBehavior<TCommand, TResponse>(
            sp.GetRequiredService<THandler>(),
            sp.GetServices<IValidator<TCommand>>()));
    }
}
