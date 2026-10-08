using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeManager.Infrastructure.Postgres.Configurations
{
    public class HabitConfiguration : IEntityTypeConfiguration<Habit>
    {
        public void Configure(EntityTypeBuilder<Habit> builder)
        {
            builder.HasKey(habit => habit.Id);

            builder.Property(habit => habit.Id)
                .ValueGeneratedOnAdd()
                .HasConversion(id => id!.Value, id => new HabitId(id));

            builder.Property(habit => habit.Name)
                .IsRequired()
                .HasMaxLength(HabitName.MaxLength)
                .HasConversion(name => name.Value, name => HabitName.FromPersistence(name));

            builder.Property(habit => habit.NormalizedName)
                .IsRequired()
                .HasMaxLength(HabitName.MaxLength);

            builder.Property(habit => habit.Description)
                .HasMaxLength(HabitDescription.MaxLength)
                .HasConversion(description => description!.Value, description => HabitDescription.FromPersistence(description));

            builder.Property(habit => habit.Trigger)
                .HasMaxLength(HabitTrigger.MaxLength)
                .HasConversion(trigger => trigger!.Value, trigger => HabitTrigger.FromPersistence(trigger));

            builder.Property(habit => habit.Kind).IsRequired();
            builder.Property(habit => habit.Difficulty).IsRequired();
            builder.Property(habit => habit.FrequencyType).IsRequired();

            // HabitWeekDays bit mask (Monday = 1 ... Sunday = 64); 0 unless the frequency is WeekDays.
            builder.Property(habit => habit.WeekDays).IsRequired();
            builder.Property(habit => habit.TimesPerWeek);

            builder.Property(habit => habit.StartDate).IsRequired();
            builder.Property(habit => habit.CreatedAt).IsRequired();
            builder.Property(habit => habit.ArchivedAt);
            builder.Property(habit => habit.CurrentStreak).IsRequired();
            builder.Property(habit => habit.LongestStreak).IsRequired();
            builder.Property(habit => habit.EvaluatedUntil).IsRequired();

            builder.Ignore(habit => habit.Frequency);
            builder.Ignore(habit => habit.IsArchived);

            // Names are unique among the user's active habits only: an archived habit frees its name.
            builder.HasIndex(habit => new { habit.UserId, habit.NormalizedName })
                .IsUnique()
                .HasFilter("\"ArchivedAt\" IS NULL");

            builder.HasIndex(habit => new { habit.UserId, habit.ArchivedAt });

            // Serves the day close's "what is behind" query: only active habits are judged.
            builder.HasIndex(habit => habit.EvaluatedUntil)
                .HasFilter("\"ArchivedAt\" IS NULL");

            // Lets LIKE '%term%' on NormalizedName use an index instead of scanning the table.
            builder.HasIndex(habit => habit.NormalizedName)
                .HasMethod("gin")
                .HasOperators("gin_trgm_ops");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(habit => habit.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
