using LifeManager.Application.Home.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.FinanceDashboard;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Home.Services
{
    /// <summary>
    /// The Home's summary of the day. A pure read: it never syncs achievements or challenges (those stay on the habits
    /// screens) and never creates the player's profile. The habits are counted by <see cref="HabitDayAgenda"/>, the
    /// same rules as <c>GET api/Habits/Today</c>. At most 6 queries (habits, check-ins, profile, month, plan, and the
    /// month's category totals only with a plan), one after the other on the request's DbContext.
    /// </summary>
    public class HomeOverviewService(
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        IPlayerProfileRepository playerProfileRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        IBudgetPlanRepository budgetPlanRepository,
        IFinanceDashboardRepository financeDashboardRepository,
        AppClock appClock)
    {
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly IBudgetPlanRepository _budgetPlanRepository = budgetPlanRepository;
        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;
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
            var budget = await GetBudgetAsync(userId, month, cancellationToken);

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

        /// <summary>The month's plan against what was spent: the same reads and rules as the budget screen.</summary>
        private async Task<HomeBudgetDto?> GetBudgetAsync(UserId userId, YearMonth month, CancellationToken cancellationToken)
        {
            var item = await _budgetPlanRepository.GetAsync(userId, month, cancellationToken);
            if (item is null)
                return null;

            var expenses = (await _financeDashboardRepository.GetCategoryMonthTotalsAsync(userId, month.FirstDay, month.LastDay, cancellationToken))
                .Where(total => total.Type == MoneyFlowType.Expense)
                .ToList();

            return new HomeBudgetDto(item.Plan.SpendingLimit, expenses.Sum(total => total.Amount), Tightest(item, expenses));
        }

        /// <summary>The expense slice with the highest spent-to-amount ratio (the free part has no slice); ties by name, then id.</summary>
        private static HomeTightestSliceDto? Tightest(BudgetPlanListItem item, IReadOnlyList<DashboardCategoryMonthTotal> expenses)
        {
            var plan = item.Plan;

            return plan.Slices
                .Where(slice => slice.Type == MoneyFlowType.Expense)
                .Select(slice =>
                {
                    var categoryId = slice.CategoryId!.Value;
                    var spent = expenses.Where(total => total.CategoryId == categoryId).Sum(total => total.Amount);

                    return new HomeTightestSliceDto(
                        categoryId,
                        item.CategoryNames.GetValueOrDefault(categoryId) ?? string.Empty,
                        decimal.Round(slice.ProgressOf(spent, plan.Total), RatioDecimalPlaces, MidpointRounding.AwayFromZero));
                })
                .OrderByDescending(slice => slice.Ratio)
                .ThenBy(slice => slice.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(slice => slice.CategoryId)
                .FirstOrDefault();
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
