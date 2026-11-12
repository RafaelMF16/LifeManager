using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Push.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Domain.UsersPreferences.Interfaces;

namespace LifeManager.Application.Habits.Reminders
{
    /// <summary>One run of the reminders: the business day and hour it judges, and the users who may get one.</summary>
    public sealed record HabitReminderRun(DateOnly Date, int Hour, IReadOnlyList<UserId> UserIds);

    /// <param name="Habits">How many habits the notification named; 0 when nothing went out.</param>
    /// <param name="Sent">Devices the push service accepted it for.</param>
    /// <param name="Removed">Dead devices (404/410) deleted.</param>
    public sealed record HabitReminderOutcome(int Habits, int Sent, int Removed)
    {
        public static readonly HabitReminderOutcome Nothing = new(0, 0, 0);
    }

    /// <summary>
    /// The habit reminders, called by the hourly <c>HabitRemindersRunner</c>: at each hour, the habits to build whose
    /// reminder is that hour and that are still due and not done today (the Today screen's agenda) become one
    /// notification per user, sent to each of the user's devices. A habit is reminded at most once a day
    /// (<see cref="IHabitReminderRepository.ReserveAsync"/> before sending); a failed send is not retried (best effort).
    /// </summary>
    public class HabitReminderService(
        IHabitReminderRepository habitReminderRepository,
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        IHabitProgressRepository habitProgressRepository,
        IPushSubscriptionRepository pushSubscriptionRepository,
        IUserPreferencesRepository userPreferencesRepository,
        IPushSender pushSender,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IHabitReminderRepository _habitReminderRepository = habitReminderRepository;
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly IHabitProgressRepository _habitProgressRepository = habitProgressRepository;
        private readonly IPushSubscriptionRepository _pushSubscriptionRepository = pushSubscriptionRepository;
        private readonly IUserPreferencesRepository _userPreferencesRepository = userPreferencesRepository;
        private readonly IPushSender _pushSender = pushSender;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>Whether reminders can go out at all (VAPID keys configured).</summary>
        public bool IsEnabled => _pushSender.PublicKey is not null;

        /// <summary>The day and hour are read once, so a run that crosses the hour still judges the hour it started in.</summary>
        public async Task<HabitReminderRun> GetDueAsync(CancellationToken cancellationToken)
        {
            var (date, hour) = _appClock.CurrentHour();

            if (!IsEnabled)
                return new HabitReminderRun(date, hour, []);

            var userIds = await _habitReminderRepository.GetUserIdsWithRemindersAtAsync(hour, cancellationToken);
            return new HabitReminderRun(date, hour, userIds);
        }

        public async Task<HabitReminderOutcome> SendForUserAsync(UserId userId, DateOnly date, int hour, CancellationToken cancellationToken)
        {
            var subscriptions = await _pushSubscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            if (subscriptions.Count == 0)
                return HabitReminderOutcome.Nothing;

            var habits = await _habitRepository.GetActiveByUserIdAsync(userId, HabitKind.Positive, cancellationToken);
            if (!habits.Any(habit => habit.ReminderHour == hour))
                return HabitReminderOutcome.Nothing;

            // The week of today: a times-per-week habit whose target is met is no longer due.
            var checkIns = await _habitCheckInRepository.GetByUserIdAsync(userId, StreakCalculator.WeekStart(date), date, cancellationToken);
            var progress = (await _habitProgressRepository.GetByUserIdAsync(userId, date, date, cancellationToken))
                .GroupBy(entry => entry.HabitId.Value)
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Value));

            var due = HabitReminders.Due(HabitDayAgenda.For(habits, [], checkIns, date), hour, progress, date);
            if (due.Count == 0)
                return HabitReminderOutcome.Nothing;

            var reserved = await _habitReminderRepository.ReserveAsync([.. due.Select(reminder => reminder.Habit.Id!)], date, cancellationToken);
            var reminders = due.Where(reminder => reserved.Contains(reminder.Habit.Id!)).ToList();
            if (reminders.Count == 0)
                return HabitReminderOutcome.Nothing;

            var language = (await _userPreferencesRepository.GetByUserIdAsync(userId, cancellationToken))?.Language ?? Language.PTBR;
            var payload = HabitReminderTexts.For(reminders, language, date, hour);

            var sent = 0;
            var removed = 0;
            foreach (var subscription in subscriptions)
            {
                var result = await _pushSender.SendAsync(new PushTarget(subscription.Endpoint, subscription.P256dh, subscription.Auth), payload, cancellationToken);

                if (result == PushSendResult.Sent)
                {
                    await _pushSubscriptionRepository.MarkSuccessAsync(subscription.Id!, userId, _timeProvider.GetUtcNow(), cancellationToken);
                    sent++;
                }
                else if (result == PushSendResult.Gone)
                {
                    await _pushSubscriptionRepository.DeleteByIdAsync(subscription.Id!, userId, cancellationToken);
                    removed++;
                }
            }

            return new HabitReminderOutcome(reminders.Count, sent, removed);
        }

        /// <summary>Logs older than <see cref="HabitReminderLog.RetentionDays"/> decide nothing anymore.</summary>
        public Task<int> DeleteOldLogsAsync(CancellationToken cancellationToken)
            => _habitReminderRepository.DeleteLogsBeforeAsync(_appClock.Today().AddDays(-HabitReminderLog.RetentionDays), cancellationToken);
    }
}
