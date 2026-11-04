using CashFlow.Transactions.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Transactions.Infrastructure.EntityConfiguration;

internal sealed class OutboxMessageMap : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(150).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.TraceParent).HasMaxLength(128);
        builder.Property(m => m.LastError).HasMaxLength(2000);

        builder.HasIndex(m => new { m.NextAttemptAt, m.CreatedAt })
            .HasFilter("processed_at IS NULL")
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
