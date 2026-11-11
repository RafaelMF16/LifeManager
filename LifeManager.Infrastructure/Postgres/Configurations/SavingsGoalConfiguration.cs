using LifeManager.Domain.SavingsGoals;
using LifeManager.Domain.SavingsGoals.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class SavingsGoalConfiguration : IEntityTypeConfiguration<SavingsGoal>
    {
        private const int AmountPrecision = 14;
        private const int AmountScale = 2;

        public void Configure(EntityTypeBuilder<SavingsGoal> builder)
        {
            builder.HasKey(goal => goal.Id);

            builder.Property(goal => goal.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new SavingsGoalId(id));

            builder.Property(goal => goal.Name)
                .IsRequired()
                .HasMaxLength(SavingsGoalName.MaxLength)
                .HasConversion(name => name.Value, name => SavingsGoalName.FromPersistence(name));

            builder.Property(goal => goal.NormalizedName)
                .IsRequired()
                .HasMaxLength(SavingsGoalName.MaxLength);

            builder.Property(goal => goal.TargetAmount).IsRequired().HasPrecision(AmountPrecision, AmountScale);
            builder.Property(goal => goal.InitialAmount).IsRequired().HasPrecision(AmountPrecision, AmountScale);
            builder.Property(goal => goal.TargetMonthStart);
            builder.Property(goal => goal.Icon).HasMaxLength(SavingsGoal.IconMaxLength);
            builder.Property(goal => goal.CreatedAt).IsRequired();
            builder.Property(goal => goal.ArchivedAt);
            builder.Property(goal => goal.CompletedAt);

            builder.Ignore(goal => goal.TargetMonth);
            builder.Ignore(goal => goal.IsArchived);

            // Names are unique among the user's active goals only: an archived goal frees its name.
            builder.HasIndex(goal => new { goal.UserId, goal.NormalizedName })
                .IsUnique()
                .HasFilter("\"ArchivedAt\" IS NULL");

            // Lets LIKE '%term%' on NormalizedName use an index instead of scanning the table.
            builder.HasIndex(goal => goal.NormalizedName)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(goal => goal.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
