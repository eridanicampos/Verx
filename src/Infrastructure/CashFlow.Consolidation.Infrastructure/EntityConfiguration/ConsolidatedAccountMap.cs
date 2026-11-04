using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Consolidation.Infrastructure.EntityConfiguration;

internal sealed class ConsolidatedAccountMap : IEntityTypeConfiguration<ConsolidatedAccount>
{
    public void Configure(EntityTypeBuilder<ConsolidatedAccount> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.AccountId);
    }
}
