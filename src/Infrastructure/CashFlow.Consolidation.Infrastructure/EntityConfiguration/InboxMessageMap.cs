using CashFlow.Consolidation.Domain.Errors;
using CashFlow.Consolidation.Domain.Models;
using CashFlow.Consolidation.Domain.Utils;
using CashFlow.Consolidation.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Consolidation.Infrastructure.EntityConfiguration;

internal sealed class InboxMessageMap : IEntityTypeConfiguration<InboxMessage>
{
    public void Configure(EntityTypeBuilder<InboxMessage> builder)
    {
        builder.ToTable("InboxMessages");
        builder.HasKey(m => m.MessageId);
        builder.Property(m => m.MessageType).HasMaxLength(150).IsRequired();
        builder.HasIndex(m => m.ProcessedAt);
    }
}
