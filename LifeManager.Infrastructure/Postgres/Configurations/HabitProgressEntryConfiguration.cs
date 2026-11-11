using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class HabitProgressEntryConfiguration : IEntityTypeConfiguration<HabitProgressEntry>
    {
        public void Configure(EntityTypeBuilder<HabitProgressEntry> builder)
        {
            builder.HasKey(entry => entry.Id);

            builder.Property(entry => entry.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new HabitProgressEntryId(id));

            builder.Property(entry => entry.HabitId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new HabitId(id));

            builder.Property(entry => entry.Date).IsRequired();
            builder.Property(entry => entry.Value).IsRequired();
            builder.Property(entry => entry.CreatedAt).IsRequired();

            // A day's entries (its total, the list to remove from) and the heatmap's totals per day.
            builder.HasIndex(entry => new { entry.HabitId, entry.Date });

            // The day's checklist reads a user's entries for today and yesterday.
            builder.HasIndex(entry => new { entry.UserId, entry.Date });

            builder.HasOne<Habit>()
                .WithMany()
                .HasForeignKey(entry => entry.HabitId)
                .OnDelete(DeleteBehavior.Cascade);

            // NO ACTION, made DEFERRABLE INITIALLY DEFERRED by migration MakeUserDeletionForeignKeysDeferrable (EF can't
            // express it): deleting a user cascades to its habits, which cascade to their entries, and Postgres checks a
            // plain NO ACTION at the end of each cascade, before that one ran. Deferred, it is checked at the commit.
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(entry => entry.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
