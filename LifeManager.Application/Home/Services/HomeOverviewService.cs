using LifeManager.Application.Home.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Home.Services
{
    /// <summary>
    /// The Home's summary of the day. A pure read: it never syncs achievements or challenges (those stay on the habits
    /// screens) and never creates the player's profile. The habits are counted by <see cref="HabitDayAgenda"/>, the
    /// same rules as <c>GET api/Habits/Today</c>. Its queries run one after the other on the request's DbContext.
    /// </summary>
    public class HomeOverviewService(
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        IPlayerProfileRepository playerProfileRepository,
        AppClock appClock)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly AppClock _appClock = appClock;

        public async Task<HomeOverviewDto> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var habits = await GetHabitsAsync(userId, today, cancellationToken);
            var profile = await _playerProfileRepository.GetByUserIdAsync(userId, cancellationToken);
            var player = profile is null ? null : new HomePlayerDto(profile.Level, profile.Hp, profile.MaxHp, profile.Coins);
            var finance = new HomeFinanceDto(YearMonth.From(today).ToString(), null, 0m, 0m, 0m, 0m, null);

            return new HomeOverviewDto(today, habits, player, finance);
        }

        private async Task<HomeHabitsDto> GetHabitsAsync(UserId userId, DateOnly today, CancellationToken cancellationToken)
        {
            var habits = await _habitRepository.GetActiveByUserIdAsync(userId, cancellationToken);
            // From the start of yesterday's week: a weekly habit's week decides whether yesterday is still pending.
            var checkIns = await _habitCheckInRepository.GetByUserIdAsync(userId, StreakCalculator.WeekStart(today.AddDays(-1)), today, cancellationToken);

            var agenda = HabitDayAgenda.For(
                habits.Where(habit => habit.Kind == HabitKind.Positive),
                habits.Where(habit => habit.Kind == HabitKind.Negative),
                checkIns,
                today);

            return new HomeHabitsDto(
                agenda.Today.Count,
                agenda.Today.Count(due => due.Done),
                agenda.YesterdayPending.Count,
                agenda.Avoiding.Count,
                agenda.Avoiding.Count(avoided => avoided.RelapsedToday),
                BestStreak(habits));
        }

        /// <summary>The longest current streak in days (a weekly habit's weeks × 7); ties by name, then id.</summary>
        private static HomeBestStreakDto? BestStreak(IEnumerable<Habit> habits)
            => habits
                .Select(habit => new HomeBestStreakDto(habit.Id!.Value, habit.Name.Value, HabitRewards.StreakBonusDays(habit, habit.CurrentStreak)))
                .Where(streak => streak.Days > 0)
                .OrderByDescending(streak => streak.Days)
                .ThenBy(streak => streak.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(streak => streak.HabitId)
                .FirstOrDefault();
    }
}
