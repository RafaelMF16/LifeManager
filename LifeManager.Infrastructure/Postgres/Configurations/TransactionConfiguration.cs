using LifeManager.Domain.Categories;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;

        public void Configure(EntityTypeBuilder<Transaction> builder)
        {
            builder.HasKey(transaction => transaction.Id);

            builder.Property(transaction => transaction.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new TransactionId(id));

            builder.Property(transaction => transaction.Type)
                .IsRequired();

            builder.Property(transaction => transaction.Amount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(amount => amount.Value, amount => TransactionAmount.FromPersistence(amount));

            builder.Property(transaction => transaction.SignedAmount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale);

            builder.Property(transaction => transaction.Description)
                .IsRequired()
                .HasMaxLength(TransactionDescription.MaxLength)
                .HasConversion(description => description.Value, description => TransactionDescription.FromPersistence(description));

            builder.Property(transaction => transaction.NormalizedDescription)
                .IsRequired()
                .HasMaxLength(TransactionDescription.MaxLength);

            builder.Property(transaction => transaction.TransactionDate)
                .IsRequired();

            builder.HasIndex(transaction => new { transaction.MonthlySummaryId, transaction.TransactionDate });

            builder.HasIndex(transaction => transaction.NormalizedDescription)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasOne<MonthlySummary>()
                .WithMany()
                .HasForeignKey(transaction => transaction.MonthlySummaryId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(transaction => transaction.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            // Deleting a recurrence keeps what it already posted, as ordinary transactions.
            builder.HasOne<RecurringTransaction>()
                .WithMany()
                .HasForeignKey(transaction => transaction.RecurringTransactionId)
                .OnDelete(DeleteBehavior.SetNull);

            // A recurrence posts at most once per month: the last guard against a double posting.
            builder.HasIndex(transaction => new { transaction.RecurringTransactionId, transaction.MonthlySummaryId })
                .IsUnique()
                .HasFilter("\"RecurringTransactionId\" IS NOT NULL");
        }
    }
}
