using CashFlow.Transactions.Domain.Enums;
using CashFlow.Transactions.Domain.Errors;
using CashFlow.Transactions.Domain.Interfaces.Repository;
using CashFlow.Transactions.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CashFlow.Transactions.Infrastructure.EntityConfiguration;

internal sealed class AccountMap : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.HolderName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.HolderDocument).HasMaxLength(512).IsRequired();
        builder.Property(a => a.HolderDocumentHash).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Balance).HasPrecision(18, 2);
        builder.Property(a => a.OverdraftLimit).HasPrecision(18, 2);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Version).IsConcurrencyToken();
        builder.HasIndex(a => a.HolderDocumentHash).IsUnique();
    }
}
