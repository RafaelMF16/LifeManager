using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class HabitCheckInConfiguration : IEntityTypeConfiguration<HabitCheckIn>
    {
        public void Configure(EntityTypeBuilder<HabitCheckIn> builder)
        {
            builder.HasKey(checkIn => checkIn.Id);

            builder.Property(checkIn => checkIn.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new HabitCheckInId(id));

            builder.Property(checkIn => checkIn.HabitId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new HabitId(id));

            builder.Property(checkIn => checkIn.Date).IsRequired();
            builder.Property(checkIn => checkIn.Status).IsRequired();
            builder.Property(checkIn => checkIn.CreatedAt).IsRequired();
            builder.Property(checkIn => checkIn.CoinsAwarded).IsRequired();
            builder.Property(checkIn => checkIn.XpAwarded).IsRequired();
            builder.Property(checkIn => checkIn.HpAwarded).IsRequired();
            builder.Property(checkIn => checkIn.FreezeAwarded).IsRequired();

            builder.Ignore(checkIn => checkIn.Awarded);
            builder.Ignore(checkIn => checkIn.IsSuccess);

            // One outcome per habit and day: the guard against a double check-in and against the day close judging a
            // day twice.
            builder.HasIndex(checkIn => new { checkIn.HabitId, checkIn.Date })
                .IsUnique();

            // The day's checklist reads a user's check-ins over a few days.
            builder.HasIndex(checkIn => new { checkIn.UserId, checkIn.Date });

            builder.HasOne<Habit>()
                .WithMany()
                .HasForeignKey(checkIn => checkIn.HabitId)
                .OnDelete(DeleteBehavior.Cascade);

            // NO ACTION: deleting a user cascades to its habits, which cascade to their check-ins.
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(checkIn => checkIn.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
