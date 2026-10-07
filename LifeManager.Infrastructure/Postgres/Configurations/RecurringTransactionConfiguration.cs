using LifeManager.Domain.Categories;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.RecurringTransactions.ValueObjects;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class RecurringTransactionConfiguration : IEntityTypeConfiguration<RecurringTransaction>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;

        public void Configure(EntityTypeBuilder<RecurringTransaction> builder)
        {
            builder.HasKey(recurringTransaction => recurringTransaction.Id);

            builder.Property(recurringTransaction => recurringTransaction.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new RecurringTransactionId(id));

            builder.Property(recurringTransaction => recurringTransaction.Type)
                .IsRequired();

            builder.Property(recurringTransaction => recurringTransaction.Amount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(amount => amount.Value, amount => TransactionAmount.FromPersistence(amount));

            builder.Property(recurringTransaction => recurringTransaction.Description)
                .IsRequired()
                .HasMaxLength(TransactionDescription.MaxLength)
                .HasConversion(description => description.Value, description => TransactionDescription.FromPersistence(description));

            builder.Property(recurringTransaction => recurringTransaction.NormalizedDescription)
                .IsRequired()
                .HasMaxLength(TransactionDescription.MaxLength);

            builder.Property(recurringTransaction => recurringTransaction.DayOfMonth)
                .IsRequired()
                .HasConversion(day => day.Value, day => RecurrenceDay.FromPersistence(day));

            // Months are stored as their first day (date columns), so they stay comparable and readable in SQL.
            builder.Property(recurringTransaction => recurringTransaction.StartMonth)
                .IsRequired()
                .HasConversion(month => month.FirstDay, date => YearMonth.From(date));

            builder.Property(recurringTransaction => recurringTransaction.EndMonth)
                .HasConversion(month => month!.FirstDay, date => YearMonth.From(date));

            builder.Property(recurringTransaction => recurringTransaction.NextMonth)
                .IsRequired()
                .HasConversion(month => month.FirstDay, date => YearMonth.From(date));

            builder.Property(recurringTransaction => recurringTransaction.IsActive)
                .IsRequired();

            builder.Property(recurringTransaction => recurringTransaction.NextOccurrenceDate);

            builder.Ignore(recurringTransaction => recurringTransaction.Status);

            // Serves the background job's "what is due" query: only active recurrences can be due.
            builder.HasIndex(recurringTransaction => recurringTransaction.NextOccurrenceDate)
                .HasFilter("\"IsActive\"");

            builder.HasIndex(recurringTransaction => recurringTransaction.UserId);

            builder.HasIndex(recurringTransaction => recurringTransaction.NormalizedDescription)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(recurringTransaction => recurringTransaction.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // NO ACTION for the same reason as Transactions: deleting a user cascades to both its categories and its
            // recurrences in one statement, and deleting a category still in use is blocked by CategoryService.
            builder.HasOne<Category>()
                .WithMany()
                .HasForeignKey(recurringTransaction => recurringTransaction.CategoryId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
