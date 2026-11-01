using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class WeeklyChallengeConfiguration : IEntityTypeConfiguration<WeeklyChallenge>
    {
        public void Configure(EntityTypeBuilder<WeeklyChallenge> builder)
        {
            builder.HasKey(challenge => challenge.Id);

            builder.Property(challenge => challenge.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new WeeklyChallengeId(id));

            builder.Property(challenge => challenge.HabitId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new HabitId(id));

            builder.Property(challenge => challenge.WeekStart).IsRequired();
            builder.Property(challenge => challenge.Type).IsRequired();
            builder.Property(challenge => challenge.Target).IsRequired();
            builder.Property(challenge => challenge.Baseline).HasPrecision(12, 2);
            builder.Property(challenge => challenge.Effort).IsRequired();
            builder.Property(challenge => challenge.RewardCoins).IsRequired();
            builder.Property(challenge => challenge.RewardXp).IsRequired();
            builder.Property(challenge => challenge.Status).IsRequired();
            builder.Property(challenge => challenge.CreatedAt).IsRequired();

            // One live challenge per habit and week: the last defense against two requests at once.
            builder.HasIndex(challenge => new { challenge.HabitId, challenge.WeekStart })
                .IsUnique()
                .HasFilter($"\"Status\" <> {(int)ChallengeStatus.Cancelled}");

            // The week's challenges, the active ones to sync and the history all start from the user.
            builder.HasIndex(challenge => new { challenge.UserId, challenge.WeekStart });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(challenge => challenge.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<Habit>()
                .WithMany()
                .HasForeignKey(challenge => challenge.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
