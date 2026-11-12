using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Push;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Habits
{
    public class HabitReminderRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly StartDate = new(2026, 11, 1);
        private static readonly DateOnly Today = new(2026, 11, 11);
        private const int Hour = 20;

        [Fact]
        public async Task GetUserIdsWithRemindersAtAsync_ShouldListUsersWithAnActiveReminderOfTheHourAndADevice()
        {
            await using var context = Fixture.CreateContext();
            var due = await context.AddUserAsync("Ana");
            var noDevice = await context.AddUserAsync("Bia");
            var otherHour = await context.AddUserAsync("Caio");
            var archived = await context.AddUserAsync("Duda");
            var leaving = await context.AddUserAsync("Eva");

            await context.AddHabitAsync(due, "Ler", StartDate, reminderHour: Hour);
            await context.AddHabitAsync(due, "Correr", StartDate, reminderHour: Hour);
            await context.AddHabitAsync(noDevice, "Ler", StartDate, reminderHour: Hour);
            await context.AddHabitAsync(otherHour, "Ler", StartDate, reminderHour: Hour + 1);
            var archivedHabit = await context.AddHabitAsync(archived, "Ler", StartDate, reminderHour: Hour);
            archivedHabit.Archive(TestData.Now);
            await context.AddHabitAsync(leaving, "Ler", StartDate, reminderHour: Hour);
            leaving.ScheduleDeletion(TestData.Now);
            await context.SaveChangesAsync();

            foreach (var user in new[] { due, otherHour, archived, leaving })
                await new PushSubscriptionRepository(Fixture.CreateContext())
                    .SaveAsync(TestData.NewPushSubscription(user, $"https://fcm.googleapis.com/fcm/send/{user.Name.Value}"), default);

            var userIds = await new HabitReminderRepository(Fixture.CreateContext()).GetUserIdsWithRemindersAtAsync(Hour, default);

            Assert.Equal([due.Id!], userIds);
        }

        [Fact]
        public async Task ReserveAsync_ShouldReturnOnlyTheHabitsNotReservedYetThatDay()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var first = await context.AddHabitAsync(user, "Ler", StartDate, reminderHour: Hour);
            var second = await context.AddHabitAsync(user, "Correr", StartDate, reminderHour: Hour);

            var firstRun = await new HabitReminderRepository(Fixture.CreateContext()).ReserveAsync([first.Id!], Today, default);
            var secondRun = await new HabitReminderRepository(Fixture.CreateContext()).ReserveAsync([first.Id!, second.Id!], Today, default);
            var nextDay = await new HabitReminderRepository(Fixture.CreateContext()).ReserveAsync([first.Id!], Today.AddDays(1), default);

            Assert.Equal([first.Id!], firstRun);
            Assert.Equal([second.Id!], secondRun);
            Assert.Equal([first.Id!], nextDay);
        }

        [Fact]
        public async Task ReserveAsync_ShouldGiveEachHabitToOneRunOnly_WhenTwoRunsReserveAtOnce()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var habitIds = new List<HabitId>();
            for (var index = 0; index < 20; index++)
                habitIds.Add((await context.AddHabitAsync(user, $"Hábito {index}", StartDate, reminderHour: Hour)).Id!);

            var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
                Task.Run(async () =>
                {
                    await using var runContext = Fixture.CreateContext();
                    return await new HabitReminderRepository(runContext).ReserveAsync(habitIds, Today, default);
                })));

            Assert.Equal(habitIds.Count, runs.Sum(run => run.Count));
            Assert.Equal(habitIds.ToHashSet(), runs.SelectMany(run => run).ToHashSet());
            Assert.Equal(habitIds.Count, await Fixture.CreateContext().HabitReminderLogs.CountAsync());
        }

        [Fact]
        public async Task DeleteLogsBeforeAsync_ShouldKeepTheLogsFromTheDateOn()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var habit = await context.AddHabitAsync(user, "Ler", StartDate, reminderHour: Hour);
            context.AddRange(new HabitReminderLog(habit.Id!, Today.AddDays(-3)), new HabitReminderLog(habit.Id!, Today.AddDays(-2)), new HabitReminderLog(habit.Id!, Today));
            await context.SaveChangesAsync();

            var deleted = await new HabitReminderRepository(Fixture.CreateContext()).DeleteLogsBeforeAsync(Today.AddDays(-2), default);

            Assert.Equal(1, deleted);
            Assert.Equal([Today.AddDays(-2), Today], await Fixture.CreateContext().HabitReminderLogs.Select(log => log.Date).OrderBy(date => date).ToListAsync());
        }
    }
}
