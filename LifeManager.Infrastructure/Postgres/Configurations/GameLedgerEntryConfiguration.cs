using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class GameLedgerEntryConfiguration : IEntityTypeConfiguration<GameLedgerEntry>
    {
        public void Configure(EntityTypeBuilder<GameLedgerEntry> builder)
        {
            builder.HasKey(entry => entry.Id);

            builder.Property(entry => entry.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new GameLedgerEntryId(id));

            builder.Property(entry => entry.Kind).IsRequired();
            builder.Property(entry => entry.OccurredOn).IsRequired();
            builder.Property(entry => entry.CreatedAt).IsRequired();
            builder.Property(entry => entry.CoinsDelta).IsRequired();
            builder.Property(entry => entry.XpDelta).IsRequired();
            builder.Property(entry => entry.HpDelta).IsRequired();

            builder.Property(entry => entry.Description)
                .HasMaxLength(GameLedgerEntry.DescriptionMaxLength);

            // The statement lists a user's entries newest first.
            builder.HasIndex(entry => new { entry.UserId, entry.CreatedAt, entry.Id });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(entry => entry.HabitId)
                .HasConversion(id => id!.Value, id => new HabitId(id));

            // SET NULL: the statement outlives the habit (habits are archived, not deleted, but a user deletion cascades).
            builder.HasOne<Habit>()
                .WithMany()
                .HasForeignKey(entry => entry.HabitId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Property(entry => entry.RewardId)
                .HasConversion(id => id!.Value, id => new RewardId(id));

            builder.HasOne<Reward>()
                .WithMany()
                .HasForeignKey(entry => entry.RewardId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
