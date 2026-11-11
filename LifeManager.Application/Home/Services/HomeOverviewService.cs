using LifeManager.Application.Home.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Home.Services
{
    /// <summary>
    /// The Home's summary of the day. A pure read: it never syncs achievements or challenges (those stay on the habits
    /// screens) and never creates the player's profile. The habits are counted by <see cref="HabitDayAgenda"/>, the
    /// same rules as <c>GET api/Habits/Today</c>. At most 6 queries (habits, check-ins, profile, month, plan, and the
    /// plan's slices with their spending, only with a plan), one after the other on the request's DbContext.
    /// </summary>
    public class HomeOverviewService(
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        IPlayerProfileRepository playerProfileRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        IBudgetPlanRepository budgetPlanRepository,
        AppClock appClock)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly IBudgetPlanRepository _budgetPlanRepository = budgetPlanRepository;
        private readonly AppClock _appClock = appClock;

        /// <summary>Decimal places of <see cref="HomeTightestSliceDto.Ratio"/>.</summary>
        public const int RatioDecimalPlaces = 4;

        public async Task<HomeOverviewDto> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var habits = await GetHabitsAsync(userId, today, cancellationToken);
            var profile = await _playerProfileRepository.GetByUserIdAsync(userId, cancellationToken);
            var player = profile is null ? null : new HomePlayerDto(profile.Level, profile.Hp, profile.MaxHp, profile.Coins);
            var finance = await GetFinanceAsync(userId, YearMonth.From(today), cancellationToken);

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

        private async Task<HomeFinanceDto> GetFinanceAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var monthlySummary = await _monthlySummaryRepository.GetByPeriodAsync(userId, month, cancellationToken);
            var budget = await GetBudgetAsync(userId, month, monthlySummary, cancellationToken);

            if (monthlySummary is null)
                return new HomeFinanceDto(month.ToString(), null, 0m, 0m, 0m, 0m, budget);

            return new HomeFinanceDto(
                month.ToString(),
                monthlySummary.Id!.Value,
                monthlySummary.TotalIncome.Value,
                monthlySummary.TotalExpense.Value,
                monthlySummary.TotalInvestment.Value,
                monthlySummary.Balance.Value,
                budget);
        }

        /// <summary>The month's plan against what was spent: the same rules as the budget screen.</summary>
        /// <param name="monthlySummary">The month, whose stored expense total is what was spent (none without it).</param>
        private async Task<HomeBudgetDto?> GetBudgetAsync(UserId userId, YearMonth month, MonthlySummary? monthlySummary, CancellationToken cancellationToken)
        {
            var spending = await _budgetPlanRepository.GetWithSliceSpendingAsync(userId, month, cancellationToken);
            if (spending is null)
                return null;

            return new HomeBudgetDto(spending.Plan.SpendingLimit, monthlySummary?.TotalExpense.Value ?? 0m, Tightest(spending));
        }

        /// <summary>
        /// The expense slice with the highest spent-to-amount ratio (the free part has no slice); ties by normalized
        /// name (ordinal, so the server's culture never changes the pick), then id.
        /// </summary>
        private static HomeTightestSliceDto? Tightest(BudgetPlanSpending spending)
        {
            var plan = spending.Plan;
            var slicesByCategory = plan.Slices
                .Where(slice => slice.CategoryId is not null)
                .ToDictionary(slice => slice.CategoryId!.Value);

            return spending.ExpenseSlices
                .Where(sliceSpending => slicesByCategory.ContainsKey(sliceSpending.CategoryId))
                .Select(sliceSpending => new HomeTightestSliceDto(
                    sliceSpending.CategoryId,
                    sliceSpending.CategoryName,
                    decimal.Round(
                        slicesByCategory[sliceSpending.CategoryId].ProgressOf(sliceSpending.Spent, plan.Total),
                        RatioDecimalPlaces,
                        MidpointRounding.AwayFromZero)))
                .OrderByDescending(slice => slice.Ratio)
                .ThenBy(slice => SearchText.Normalize(slice.Name), StringComparer.Ordinal)
                .ThenBy(slice => slice.CategoryId)
                .FirstOrDefault();
        }

        /// <summary>The longest current streak in days (a weekly habit's weeks × 7); ties by normalized name (ordinal), then id.</summary>
        private static HomeBestStreakDto? BestStreak(IEnumerable<Habit> habits)
            => habits
                .Select(habit => (Habit: habit, Days: HabitRewards.StreakBonusDays(habit, habit.CurrentStreak)))
                .Where(streak => streak.Days > 0)
                .OrderByDescending(streak => streak.Days)
                .ThenBy(streak => streak.Habit.NormalizedName, StringComparer.Ordinal)
                .ThenBy(streak => streak.Habit.Id!.Value)
                .Select(streak => new HomeBestStreakDto(streak.Habit.Id!.Value, streak.Habit.Name.Value, streak.Days))
                .FirstOrDefault();
    }
}
