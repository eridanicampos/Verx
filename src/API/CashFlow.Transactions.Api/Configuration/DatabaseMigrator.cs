using CashFlow.Transactions.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Transactions.Api.Configuration;

internal static class DatabaseMigrator
{
    public static async Task MigrateIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
        await db.Database.MigrateAsync();
    }
}
