using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Consolidation.Infrastructure.EntityConfiguration;

internal sealed class LedgerEntryMap : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries");
        builder.HasKey(e => e.TransactionId);
        builder.Property(e => e.SignedAmount).HasPrecision(18, 2);
        builder.HasIndex(e => new { e.AccountId, e.BusinessDate, e.OccurredAt });
    }
}
