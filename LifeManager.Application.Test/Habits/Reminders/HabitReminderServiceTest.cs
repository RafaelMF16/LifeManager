using LifeManager.Application.Habits.Reminders;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Push.Mocks;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Push;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits.Reminders
{
    [Collection("ApplicationServices")]
    public class HabitReminderServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        // Wednesday, 20:05 in São Paulo (23:05 UTC).
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateTimeOffset UtcNow = new(2026, 10, 7, 23, 5, 0, TimeSpan.Zero);
        private const int Hour = 20;
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private readonly HabitReminderService _service;

        public HabitReminderServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HabitReminderService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).UtcNow = UtcNow;

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            HabitReminderLogSingleton.Instance.Clear();
            PushSubscriptionSingleton.Instance.Clear();
            UserPreferencesSingleton.Instance.Clear();
            PushSenderMock.Reset();
        }

        private static Habit SeedHabit(
            string name = "Ler",
            int? reminderHour = Hour,
            UserId? userId = null,
            HabitKind kind = HabitKind.Positive,
            int? targetValue = null,
            string? targetUnit = null,
            bool archived = false)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, name, null, null, kind, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, null, StartDate, CreatedAt,
                archived ? CreatedAt : null, currentStreak: 0, longestStreak: 0, evaluatedUntil: Today.AddDays(-2),
                targetValue: targetValue, targetUnit: targetUnit, reminderHour: kind == HabitKind.Positive ? reminderHour : null);
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static PushSubscription SeedDevice(string device = "phone", UserId? userId = null)
        {
            var subscription = PushSubscription.FromPersistence(
                PushSubscriptionSingleton.Instance.Count + 1, (userId ?? UserId).Value, $"https://fcm.googleapis.com/fcm/send/{device}",
                "p256dh", "auth", null, CreatedAt, null);
            PushSubscriptionSingleton.Instance.Add(subscription);

            return subscription;
        }

        private static void SeedDone(Habit habit, DateOnly date)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, HabitCheckInStatus.Done, CreatedAt, 5, 10, 1));

        private static void SeedLanguage(Language language, UserId? userId = null)
        {
            var preferences = UserPreferences.CreateDefault(userId ?? UserId, language: language);
            preferences.AssignId(UserPreferencesSingleton.Instance.Count + 1);
            UserPreferencesSingleton.Instance.Add(preferences);
        }

        private async Task<HabitReminderRun> RunAsync()
        {
            var run = await _service.GetDueAsync(CancellationToken.None);
            foreach (var userId in run.UserIds)
                await _service.SendForUserAsync(userId, run.Date, run.Hour, CancellationToken.None);

            return run;
        }

        [Fact]
        public async Task GetDueAsync_ShouldUseTheBusinessDayAndHour()
        {
            var run = await _service.GetDueAsync(CancellationToken.None);

            Assert.Equal(Today, run.Date);
            Assert.Equal(Hour, run.Hour);
        }

        [Fact]
        public async Task GetDueAsync_ShouldListOnlyUsersWithAReminderOfThisHourAndADevice()
        {
            SeedHabit(userId: UserId);
            SeedDevice(userId: UserId);
            SeedHabit(userId: OtherUserId);                        // no device
            SeedHabit(userId: new UserId(3), reminderHour: Hour + 1);
            SeedDevice("three", new UserId(3));
            SeedHabit(userId: new UserId(4), archived: true);
            SeedDevice("four", new UserId(4));

            var run = await _service.GetDueAsync(CancellationToken.None);

            Assert.Equal([UserId], run.UserIds);
        }

        [Fact]
        public async Task GetDueAsync_ShouldListNobody_WhenPushIsNotConfigured()
        {
            SeedHabit();
            SeedDevice();
            PushSenderMock.Enabled = false;

            Assert.Empty((await _service.GetDueAsync(CancellationToken.None)).UserIds);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldNotifyEveryDeviceOfTheUser_WithTheHabitsStillToDo()
        {
            SeedHabit("Ler");
            SeedDevice("phone");
            SeedDevice("laptop");

            await RunAsync();

            Assert.Equal(2, PushSenderMock.Sent.Count);
            Assert.All(PushSenderMock.Sent, sent => Assert.Equal("Ler: ainda dá tempo hoje", sent.Payload.Body));
            Assert.All(PushSubscriptionSingleton.Instance, subscription => Assert.Equal(UtcNow, subscription.LastSuccessAt));
        }

        [Fact]
        public async Task SendForUserAsync_ShouldNotNotify_WhenTheHabitWasAlreadyDoneToday()
        {
            var habit = SeedHabit();
            SeedDevice();
            SeedDone(habit, Today);

            await RunAsync();

            Assert.Empty(PushSenderMock.Sent);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldGroupTheHabitsOfTheHourInOneNotification()
        {
            SeedHabit("Ler");
            SeedHabit("Academia");
            SeedHabit("Meditar", reminderHour: Hour - 1);
            SeedDevice();

            await RunAsync();

            Assert.Equal("Faltam 2 hábitos: Academia, Ler", Assert.Single(PushSenderMock.Sent).Payload.Body);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldTellWhatIsLeft_ForAHabitWithATarget()
        {
            var habit = SeedHabit("Beber água", targetValue: 2000, targetUnit: "ml");
            HabitProgressEntrySingleton.Instance.Add(HabitProgressEntry.FromPersistence(1, habit.Id!.Value, UserId.Value, Today, 1250, CreatedAt));
            HabitProgressEntrySingleton.Instance.Add(HabitProgressEntry.FromPersistence(2, habit.Id!.Value, UserId.Value, Today.AddDays(-1), 500, CreatedAt));
            SeedDevice();

            await RunAsync();

            Assert.Equal("Beber água: faltam 750 ml", Assert.Single(PushSenderMock.Sent).Payload.Body);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldWriteInTheUsersLanguage()
        {
            SeedHabit("Read");
            SeedDevice();
            SeedLanguage(Language.EN);

            await RunAsync();

            var sent = Assert.Single(PushSenderMock.Sent);
            Assert.Equal("Read: there's still time today", sent.Payload.Body);
            Assert.Equal("habits-2026-10-07-20", sent.Payload.Tag);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldNotNotifyTwice_WhenTheJobRunsAgainInTheSameHour()
        {
            SeedHabit();
            SeedDevice();

            await RunAsync();
            await RunAsync();

            Assert.Single(PushSenderMock.Sent);
            Assert.Single(HabitReminderLogSingleton.Instance);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldDeleteADeadDevice_AndStillNotifyTheOthers()
        {
            SeedHabit();
            var dead = SeedDevice("old-phone");
            SeedDevice("phone");
            PushSenderMock.ResultFor[dead.Endpoint] = PushSendResult.Gone;

            await RunAsync();

            Assert.Single(PushSenderMock.Sent);
            Assert.Equal("https://fcm.googleapis.com/fcm/send/phone", Assert.Single(PushSubscriptionSingleton.Instance).Endpoint);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldKeepADeviceThatFailed()
        {
            SeedHabit();
            var device = SeedDevice();
            PushSenderMock.ResultFor[device.Endpoint] = PushSendResult.Failed;

            await RunAsync();

            Assert.Empty(PushSenderMock.Sent);
            Assert.Null(Assert.Single(PushSubscriptionSingleton.Instance).LastSuccessAt);
        }

        [Fact]
        public async Task SendForUserAsync_ShouldOnlyNotifyTheUsersOwnDevices()
        {
            SeedHabit(userId: UserId);
            SeedDevice("mine", UserId);
            SeedDevice("theirs", OtherUserId);

            await RunAsync();

            Assert.Equal("https://fcm.googleapis.com/fcm/send/mine", Assert.Single(PushSenderMock.Sent).Target.Endpoint);
        }

        [Fact]
        public async Task DeleteOldLogsAsync_ShouldDeleteOnlyTheLogsBeforeTheRetention()
        {
            HabitReminderLogSingleton.Instance.Add(new HabitReminderLog(new HabitId(1), Today.AddDays(-HabitReminderLog.RetentionDays - 1)));
            HabitReminderLogSingleton.Instance.Add(new HabitReminderLog(new HabitId(1), Today.AddDays(-HabitReminderLog.RetentionDays)));
            HabitReminderLogSingleton.Instance.Add(new HabitReminderLog(new HabitId(1), Today));

            await _service.DeleteOldLogsAsync(CancellationToken.None);

            Assert.Equal([Today.AddDays(-HabitReminderLog.RetentionDays), Today], HabitReminderLogSingleton.Instance.Select(log => log.Date).Order());
        }
    }
}
