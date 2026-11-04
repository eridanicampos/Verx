using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Infrastructure.Context;

public sealed class ConsolidationDbContext : DbContext
{
    public const string Schema = "consolidation";

    public ConsolidationDbContext(DbContextOptions<ConsolidationDbContext> options)
        : base(options)
    {
    }

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    public DbSet<DailyBalance> DailyBalances => Set<DailyBalance>();

    public DbSet<ConsolidatedAccount> Accounts => Set<ConsolidatedAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConsolidationDbContext).Assembly);
        modelBuilder.UseSnakeCaseNames();
    }
}
