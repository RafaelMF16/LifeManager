using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class HabitReminderLogConfiguration : IEntityTypeConfiguration<HabitReminderLog>
    {
        public void Configure(EntityTypeBuilder<HabitReminderLog> builder)
        {
            // The key is the lock against a second reminder: INSERT … ON CONFLICT DO NOTHING on it.
            builder.HasKey(log => new { log.HabitId, log.Date });

            builder.Property(log => log.HabitId)
                .IsRequired()
                .HasConversion(id => id.Value, id => new HabitId(id));

            builder.Property(log => log.Date).IsRequired();

            // The cleanup deletes by date.
            builder.HasIndex(log => log.Date);

            builder.HasOne<Habit>()
                .WithMany()
                .HasForeignKey(log => log.HabitId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
