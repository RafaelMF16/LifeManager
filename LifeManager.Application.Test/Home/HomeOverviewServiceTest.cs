using LifeManager.Application.Habits.Services;
using LifeManager.Application.Home.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Home
{
    [Collection("ApplicationServices")]
    public class HomeOverviewServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        // Wednesday; its week runs from Monday 2026-10-05.
        private static readonly DateOnly Today = new(2026, 10, 7);
        private static readonly DateOnly Yesterday = Today.AddDays(-1);
        private static readonly DateOnly StartDate = new(2026, 9, 1);
        private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        private const int MarketCategoryId = 1;
        private const int HousingCategoryId = 2;
        private const int LeisureCategoryId = 3;
        private static readonly YearMonth October = YearMonth.From(Today);

        private readonly HomeOverviewService _service;
        private int _nextTransactionId = 1;

        public HomeOverviewServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<HomeOverviewService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            WeeklyChallengeSingleton.Instance.Clear();
            AchievementUnlockSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
            BudgetPlanSingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
        }

        private static Habit SeedHabit(
            string name,
            HabitKind kind = HabitKind.Positive,
            HabitFrequencyType frequencyType = HabitFrequencyType.Daily,
            HabitWeekDays weekDays = HabitWeekDays.None,
            int? timesPerWeek = null,
            UserId? userId = null,
            int currentStreak = 0,
            bool archived = false)
        {
            var habit = Habit.FromPersistence(
                HabitSingleton.Instance.Count + 1, (userId ?? UserId).Value, name, null, null, kind, HabitDifficulty.Easy,
                frequencyType, weekDays, timesPerWeek, StartDate, CreatedAt,
                archived ? CreatedAt : null, currentStreak, longestStreak: currentStreak, evaluatedUntil: Today.AddDays(-2));
            HabitSingleton.Instance.Add(habit);

            return habit;
        }

        private static void SeedCheckIn(Habit habit, DateOnly date, HabitCheckInStatus status = HabitCheckInStatus.Done)
            => HabitCheckInSingleton.Instance.Add(HabitCheckIn.FromPersistence(
                HabitCheckInSingleton.Instance.Count + 1, habit.Id!.Value, habit.UserId.Value, date, status, CreatedAt, 0, 0, 0));

        private static void SeedProfile(UserId userId, int coins, int hp = GameRules.MaxHp)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(
                PlayerProfileSingleton.Instance.Count + 1, userId.Value, totalXp: 0, hp, GameRules.MaxHp, coins, 0));

        [Fact]
        public async Task GetAsync_ShouldReturnAnEmptyOverview_WhenTheUserHasNothingYet()
        {
            var overview = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(Today, overview.Today);
            Assert.Equal(0, overview.Habits.DueToday);
            Assert.Equal(0, overview.Habits.DoneToday);
            Assert.Equal(0, overview.Habits.YesterdayPending);
            Assert.Equal(0, overview.Habits.Avoiding);
            Assert.Equal(0, overview.Habits.RelapsedToday);
            Assert.Null(overview.Habits.BestStreak);
            Assert.Null(overview.Player);
            Assert.Equal("2026-10", overview.Finance.Month);
            Assert.Null(overview.Finance.MonthlySummaryId);
            Assert.Null(overview.Finance.Budget);
        }

        [Fact]
        public async Task GetAsync_ShouldCountTheHabitsLikeTheTodayScreen()
        {
            var pending = SeedHabit("Pending");
            var done = SeedHabit("Done");
            SeedCheckIn(done, Today);
            SeedCheckIn(done, Yesterday);
            SeedHabit("Mondays", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);
            var weekly = SeedHabit("Weekly", frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 2);
            SeedCheckIn(weekly, Today.AddDays(-2));
            var sugar = SeedHabit("Sugar", kind: HabitKind.Negative);
            SeedCheckIn(sugar, Today, HabitCheckInStatus.Relapse);
            SeedHabit("Soda", kind: HabitKind.Negative);
            SeedHabit("Sunday soda", kind: HabitKind.Negative, frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Sunday);
            SeedCheckIn(pending, Today.AddDays(-2));

            var overview = await _service.GetAsync(UserId, CancellationToken.None);
            var today = (await ServiceProvider.GetRequiredService<HabitCheckInService>().GetTodayAsync(UserId, CancellationToken.None)).Value!;

            Assert.Equal(today.Today.Count, overview.Habits.DueToday);
            Assert.Equal(today.Today.Count(item => item.Done), overview.Habits.DoneToday);
            Assert.Equal(today.YesterdayPending.Count, overview.Habits.YesterdayPending);
            Assert.Equal(today.Avoiding.Count, overview.Habits.Avoiding);
            Assert.Equal(today.Avoiding.Count(item => item.RelapsedToday), overview.Habits.RelapsedToday);
            // And the numbers themselves, so a regression in both can't pass.
            Assert.Equal((3, 1, 2, 2, 1), (overview.Habits.DueToday, overview.Habits.DoneToday, overview.Habits.YesterdayPending, overview.Habits.Avoiding, overview.Habits.RelapsedToday));
        }

        [Fact]
        public async Task GetAsync_ShouldLeaveOutAHabitScheduledOnAnotherDay()
        {
            SeedHabit("Mondays", frequencyType: HabitFrequencyType.WeekDays, weekDays: HabitWeekDays.Monday);

            var overview = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(0, overview.Habits.DueToday);
        }

        [Fact]
        public async Task GetAsync_ShouldPickTheLongestCurrentStreak_CountingAWeeklyHabitsWeeksAsDays()
        {
            SeedHabit("Read", currentStreak: 12);
            var weekly = SeedHabit("Gym", frequencyType: HabitFrequencyType.TimesPerWeek, timesPerWeek: 3, currentStreak: 2);
            SeedHabit("Sugar", kind: HabitKind.Negative, currentStreak: 9);
            SeedHabit("Old", currentStreak: 40, archived: true);

            var overview = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(new(weekly.Id!.Value, "Gym", 14), overview.Habits.BestStreak);
        }

        [Fact]
        public async Task GetAsync_ShouldBreakAStreakTieByName()
        {
            SeedHabit("Write", currentStreak: 5);
            var read = SeedHabit("Read", currentStreak: 5);

            var overview = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(read.Id!.Value, overview.Habits.BestStreak?.HabitId);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnThePlayer()
        {
            SeedProfile(UserId, coins: 240, hp: 80);

            var overview = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(new(1, 80, GameRules.MaxHp, 240), overview.Player);
        }

        [Fact]
        public async Task GetAsync_ShouldNeverReadAnotherUsersData()
        {
            var theirs = SeedHabit("Read", userId: OtherUserId, currentStreak: 30);
            SeedCheckIn(theirs, Today);
            SeedProfile(OtherUserId, coins: 999);

            var overview = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(0, overview.Habits.DueToday);
            Assert.Null(overview.Habits.BestStreak);
            Assert.Null(overview.Player);
        }

        private static MonthlySummary SeedMonth(UserId userId, int year, int month, decimal income = 0, decimal expense = 0, decimal investment = 0)
        {
            var monthlySummary = MonthlySummary.FromPersistence(MonthlySummarySingleton.Instance.Count + 1, userId.Value, month, year, income, expense, investment);
            MonthlySummarySingleton.Instance.Add(monthlySummary);

            return monthlySummary;
        }

        private void SeedExpense(MonthlySummary month, int categoryId, decimal amount, MoneyFlowType type = MoneyFlowType.Expense)
            => TransactionSingleton.Instance.Add(Transaction.FromPersistence(
                _nextTransactionId++, month.Id!.Value, type, categoryId, amount, "description", new DateOnly(month.Year.Value, month.Month.Value, 1)));

        private static void SeedCategory(int id, string name, UserId? userId = null)
        {
            var category = Category.Create((userId ?? UserId).Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }

        private static void SeedPlan(UserId userId, YearMonth month, decimal total, params BudgetSlice[] slices)
        {
            var plan = BudgetPlan.Create(userId.Value, month, total, slices, DateTimeOffset.UnixEpoch).Value!;
            plan.AssignId(BudgetPlanSingleton.Instance.Count + 1);
            BudgetPlanSingleton.Instance.Add(plan);
        }

        private static BudgetSlice ExpenseSlice(int categoryId, decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Expense, categoryId, percent).Value!;

        private static BudgetSlice InvestmentSlice(decimal percent)
            => BudgetSlice.Create(MoneyFlowType.Investment, null, percent).Value!;

        [Fact]
        public async Task GetAsync_ShouldReturnTheCurrentMonthsTotals()
        {
            SeedMonth(UserId, 2026, 9, income: 1m);
            var october = SeedMonth(UserId, 2026, 10, income: 8000m, expense: 3120.50m, investment: 1000m);

            var finance = (await _service.GetAsync(UserId, CancellationToken.None)).Finance;

            Assert.Equal("2026-10", finance.Month);
            Assert.Equal(october.Id!.Value, finance.MonthlySummaryId);
            Assert.Equal((8000m, 3120.50m, 1000m, 3879.50m), (finance.Income, finance.Expense, finance.Investment, finance.Balance));
            Assert.Null(finance.Budget);
        }

        [Fact]
        public async Task GetAsync_ShouldPointAtTheTightestExpenseSlice_LeavingTheFreePartAndTheInvestmentOut()
        {
            SeedCategory(MarketCategoryId, "Mercado");
            SeedCategory(HousingCategoryId, "Moradia");
            SeedCategory(LeisureCategoryId, "Lazer");
            // Total 6000: market 25% = 1500, housing 30% = 1800, investment 20% = 1200, free 25%.
            SeedPlan(UserId, October, 6000m, ExpenseSlice(MarketCategoryId, 25m), ExpenseSlice(HousingCategoryId, 30m), InvestmentSlice(20m));
            var october = SeedMonth(UserId, 2026, 10, expense: 3500m, investment: 2000m);
            SeedExpense(october, MarketCategoryId, 1380m);
            SeedExpense(october, HousingCategoryId, 1620m);
            // Spending without a slice (the free part) is far past its share, and so is the investment: neither counts.
            SeedExpense(october, LeisureCategoryId, 500m);
            SeedExpense(october, MarketCategoryId, 2000m, MoneyFlowType.Investment);

            var budget = (await _service.GetAsync(UserId, CancellationToken.None)).Finance.Budget;

            Assert.NotNull(budget);
            Assert.Equal(4800m, budget.SpendingLimit);
            Assert.Equal(3500m, budget.Spent);
            Assert.Equal(new(MarketCategoryId, "Mercado", 0.92m), budget.Tightest);
        }

        [Fact]
        public async Task GetAsync_ShouldHaveNoTightestSlice_WhenThePlanOnlyHasAnInvestmentSlice()
        {
            SeedPlan(UserId, October, 5000m, InvestmentSlice(10m));

            var budget = (await _service.GetAsync(UserId, CancellationToken.None)).Finance.Budget;

            Assert.NotNull(budget);
            Assert.Equal(4500m, budget.SpendingLimit);
            Assert.Equal(0m, budget.Spent);
            Assert.Null(budget.Tightest);
        }

        [Fact]
        public async Task GetAsync_ShouldIgnoreAnotherMonthsPlanAndAnotherUsersMonth()
        {
            SeedPlan(UserId, October.AddMonths(-1), 5000m, InvestmentSlice(10m));
            SeedPlan(OtherUserId, October, 5000m, InvestmentSlice(10m));
            SeedMonth(OtherUserId, 2026, 10, income: 100m);

            var finance = (await _service.GetAsync(UserId, CancellationToken.None)).Finance;

            Assert.Null(finance.MonthlySummaryId);
            Assert.Equal(0m, finance.Income);
            Assert.Null(finance.Budget);
        }

        [Fact]
        public async Task GetAsync_ShouldWriteNothing()
        {
            var habit = SeedHabit("Read", currentStreak: 3);
            SeedCheckIn(habit, Yesterday);
            SeedProfile(UserId, coins: 10);
            SeedCategory(MarketCategoryId, "Mercado");
            SeedPlan(UserId, October, 6000m, ExpenseSlice(MarketCategoryId, 25m));
            SeedExpense(SeedMonth(UserId, 2026, 10, expense: 100m), MarketCategoryId, 100m);
            var before = Snapshot();

            await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(before, Snapshot());
        }

        private static (int, int, int, int, int, int, int, int, int, int, decimal) Snapshot()
            => (HabitSingleton.Instance.Count, HabitCheckInSingleton.Instance.Count, PlayerProfileSingleton.Instance.Count,
                GameLedgerEntrySingleton.Instance.Count, WeeklyChallengeSingleton.Instance.Count, AchievementUnlockSingleton.Instance.Count,
                MonthlySummarySingleton.Instance.Count, TransactionSingleton.Instance.Count, BudgetPlanSingleton.Instance.Count,
                PlayerProfileSingleton.Instance.Sum(profile => profile.Coins), MonthlySummarySingleton.Instance.Sum(month => month.BalanceAmount));
    }
}
