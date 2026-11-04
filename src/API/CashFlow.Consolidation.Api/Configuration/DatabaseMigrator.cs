using CashFlow.Consolidation.Infrastructure.Context;
using CashFlow.Consolidation.Infrastructure.Repository;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Api.Configuration;

internal static class DatabaseMigrator
{
    public static async Task MigrateIfEnabledAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        await db.Database.MigrateAsync();
    }
}
