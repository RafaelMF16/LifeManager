using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class MonthlySummaryConfiguration : IEntityTypeConfiguration<MonthlySummary>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;

        public void Configure(EntityTypeBuilder<MonthlySummary> builder)
        {
            builder.HasKey(monthlySummary => monthlySummary.Id);

            builder.Property(monthlySummary => monthlySummary.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new MonthlySummaryId(id));

            builder.Property(monthlySummary => monthlySummary.Month)
                .IsRequired()
                .HasConversion(month => month.Value, month => MonthlySummaryMonth.FromPersistence(month));

            builder.Property(monthlySummary => monthlySummary.Year)
                .IsRequired()
                .HasConversion(year => year.Value, year => MonthlySummaryYear.FromPersistence(year));

            builder.Property(monthlySummary => monthlySummary.TotalIncome)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(totalIncome => totalIncome.Value, totalIncome => TotalIncome.FromPersistence(totalIncome));

            builder.Property(monthlySummary => monthlySummary.TotalExpense)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale)
                .HasConversion(totalExpense => totalExpense.Value, totalExpense => TotalExpense.FromPersistence(totalExpense));

            builder.Property(monthlySummary => monthlySummary.BalanceAmount)
                .IsRequired()
                .HasPrecision(AmountPrecision, AmountScale);

            builder.Ignore(monthlySummary => monthlySummary.Balance);

            // One summary per user and month; also serves the default chronological listing and the year filter.
            builder.HasIndex(monthlySummary => new { monthlySummary.UserId, monthlySummary.Year, monthlySummary.Month })
                .IsUnique();

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(monthlySummary => monthlySummary.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
