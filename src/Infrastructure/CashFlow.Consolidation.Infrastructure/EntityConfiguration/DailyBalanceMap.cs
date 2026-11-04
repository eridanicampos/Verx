using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Consolidation.Infrastructure.EntityConfiguration;

internal sealed class DailyBalanceMap : IEntityTypeConfiguration<DailyBalance>
{
    public void Configure(EntityTypeBuilder<DailyBalance> builder)
    {
        builder.ToTable("DailyBalances");
        builder.HasKey(d => new { d.AccountId, d.Date });
        builder.Property(d => d.TotalCredits).HasPrecision(20, 2);
        builder.Property(d => d.TotalDebits).HasPrecision(20, 2);
        builder.Ignore(d => d.Net);
    }
}
