using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Transactions.Infrastructure.EntityConfiguration;

internal sealed class TransactionMap : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(10);
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.BalanceAfter).HasPrecision(18, 2);
        builder.Property(t => t.Description).HasMaxLength(250);
        builder.Property(t => t.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(t => t.RequestFingerprint).HasMaxLength(64).IsRequired();
        builder.Ignore(t => t.SignedAmount);

        builder.HasOne<Account>().WithMany().HasForeignKey(t => t.AccountId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.AccountId, t.IdempotencyKey }).IsUnique();
        builder.HasIndex(t => new { t.AccountId, t.OccurredAt });
    }
}
