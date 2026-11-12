using LifeManager.Domain.Habits.Enums;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Test.Seed;

namespace LifeManager.Infrastructure.Test.Habits
{
    public class HabitRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly StartDate = new(2026, 11, 1);

        [Fact]
        public async Task GetActiveByUserIdAsync_ShouldReturnBothKindsByName_WithoutArchivedHabitsOrOtherUsers()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var otherUser = await context.AddUserAsync("Bia");
            await context.AddHabitAsync(user, "Ler", StartDate);
            await context.AddHabitAsync(user, "Açúcar", StartDate, HabitKind.Negative);
            var archived = await context.AddHabitAsync(user, "Correr", StartDate);
            archived.Archive(TestData.Now);
            await context.SaveChangesAsync();
            await context.AddHabitAsync(otherUser, "Meditar", StartDate);

            await using var readContext = Fixture.CreateContext();
            var habits = await new HabitRepository(readContext).GetActiveByUserIdAsync(user.Id!, default);

            Assert.Equal(["Açúcar", "Ler"], habits.Select(habit => habit.Name.Value));
        }

        [Fact]
        public async Task UpdateAsync_ShouldStoreAndClearTheReminderHour()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var habit = await context.AddHabitAsync(user, "Ler", StartDate);

            habit.Update("Ler", null, null, HabitDifficulty.Medium, HabitFrequencyType.Daily, HabitWeekDays.None, null, reminderHour: 20);
            await using (var writeContext = Fixture.CreateContext())
                await new HabitRepository(writeContext).UpdateAsync(habit, default);

            await using (var readContext = Fixture.CreateContext())
                Assert.Equal(20, (await new HabitRepository(readContext).GetByIdAsync(habit.Id!, user.Id!, default))!.ReminderHour);

            habit.Update("Ler", null, null, HabitDifficulty.Medium, HabitFrequencyType.Daily, HabitWeekDays.None, null);
            await using (var writeContext = Fixture.CreateContext())
                await new HabitRepository(writeContext).UpdateAsync(habit, default);

            await using (var readContext = Fixture.CreateContext())
                Assert.Null((await new HabitRepository(readContext).GetByIdAsync(habit.Id!, user.Id!, default))!.ReminderHour);
        }
    }
}
