using LifeManager.Application.Achievements.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Achievements.Interfaces;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Achievements.Services
{
    /// <summary>
    /// Unlocks the achievements the player's history reaches. Runs when the profile is read (the frontend reloads it
    /// after every change), so it needs no hook in the features that change the game and counts the past too. Each
    /// unlock pays its bonus once, in the ledger (<see cref="GameLedgerEntryKind.AchievementUnlocked"/>), and is never
    /// taken back. The Finance family reads the user's closed months from the money side (<see cref="FinanceMonthProgress"/>).
    /// </summary>
    public class AchievementService(
        IAchievementRepository achievementRepository,
        IBudgetPlanRepository budgetPlanRepository,
        IFinanceDashboardRepository financeDashboardRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IAchievementRepository _achievementRepository = achievementRepository;
        private readonly IBudgetPlanRepository _budgetPlanRepository = budgetPlanRepository;
        private readonly IFinanceDashboardRepository _financeDashboardRepository = financeDashboardRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Unlocks what's new, paying the bonuses. Reads without the lock first, so a read with nothing new costs no
        /// lock; the unlock reads the game's numbers again under it. The Finance counts are read once, outside the lock:
        /// money never changes the game, so the lock has nothing to protect there.
        /// </summary>
        public async Task<AchievementSyncResult> SyncAsync(UserId userId, CancellationToken cancellationToken)
        {
            var finance = await GetFinanceCountsAsync(userId, cancellationToken);
            var progress = (await _achievementRepository.GetProgressAsync(userId, cancellationToken)).WithFinance(finance);
            var unlocks = await _achievementRepository.GetUnlocksAsync(userId, cancellationToken);
            if (AchievementCatalog.NewlyUnlocked(progress, Codes(unlocks)).Count == 0)
                return new AchievementSyncResult(progress, unlocks, []);

            var today = _appClock.Today();
            var now = _timeProvider.GetUtcNow();

            // decide always runs, so these end up as what the lock saw, plus what it added.
            var finalProgress = progress;
            IReadOnlyList<AchievementUnlock> lockedUnlocks = unlocks;

            var newUnlocks = await _achievementRepository.UnlockAsync(userId, (lockedProfile, lockedProgress, storedUnlocks) =>
            {
                lockedUnlocks = [.. storedUnlocks];
                var unlocked = new HashSet<AchievementCode>(Codes(storedUnlocks));
                var created = new List<AchievementUnlock>();
                var entries = new List<GameLedgerEntry>();

                // A bonus's XP can reach a level achievement, so look again until nothing is new: each turn unlocks at
                // least one more code, so it stops within the catalog's size.
                var current = lockedProgress.WithFinance(finance);
                for (var codes = AchievementCatalog.NewlyUnlocked(current, unlocked); codes.Count > 0; codes = AchievementCatalog.NewlyUnlocked(current, unlocked))
                {
                    foreach (var code in codes)
                    {
                        var outcome = lockedProfile.Apply(AchievementCatalog.Bonus(AchievementCatalog.Tier(code)));
                        created.Add(AchievementUnlock.Create(userId, code, today, now));
                        entries.AddRange(GameLedgerEntry.FromOutcome(
                            userId, GameLedgerEntryKind.AchievementUnlocked, today, now, null, outcome, achievementCode: code));
                        unlocked.Add(code);
                    }

                    current = current with { Level = lockedProfile.Level };
                }

                finalProgress = current;
                return created.Count == 0 ? null : new AchievementUnlockDecision(created, entries);
            }, cancellationToken);

            return new AchievementSyncResult(finalProgress, [.. lockedUnlocks, .. newUnlocks], newUnlocks);
        }

        /// <summary>
        /// Counts the user's closed months for the Finance achievements, from the first year with a month through the
        /// month before today's. Months come from the same per-category totals the dashboard reads.
        /// </summary>
        private async Task<FinanceMonthCounts> GetFinanceCountsAsync(UserId userId, CancellationToken cancellationToken)
        {
            var years = await _monthlySummaryRepository.GetYearsByUserIdAsync(userId, cancellationToken);
            if (years.Count == 0)
                return new FinanceMonthCounts(0, 0, 0);

            var lastClosed = YearMonth.From(_appClock.Today()).AddMonths(-1);
            var first = YearMonth.From(new DateOnly(years.Min(), 1, 1));
            if (first.Ordinal > lastClosed.Ordinal)
                return new FinanceMonthCounts(0, 0, 0);

            var totals = await _financeDashboardRepository.GetCategoryMonthTotalsAsync(userId, first.FirstDay, lastClosed.LastDay, cancellationToken);
            var plans = await _budgetPlanRepository.GetOverlappingAsync(userId, first, lastClosed, cancellationToken);

            var months = totals
                .GroupBy(total => (total.Year, total.Month))
                .Select(month => new FinanceMonth(
                    YearMonth.From(new DateOnly(month.Key.Year, month.Key.Month, 1)),
                    month.Where(total => total.Type == MoneyFlowType.Income).Sum(total => total.Amount),
                    month.Where(total => total.Type == MoneyFlowType.Expense).Sum(total => total.Amount),
                    month.Where(total => total.Type == MoneyFlowType.Investment).Sum(total => total.Amount)))
                .ToList();

            return FinanceMonthProgress.Count(
                months,
                [.. plans.Select(item => new FinanceMonthPlan(item.Plan, _appClock.DateOf(item.Plan.CreatedAt), _appClock.DateOf(item.Plan.UpdatedAt)))],
                lastClosed);
        }

        /// <summary>Syncs, then lists the whole catalog with the player's progress and unlock dates.</summary>
        public async Task<AchievementsResponseDto> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            var sync = await SyncAsync(userId, cancellationToken);
            var unlocks = sync.Unlocks.ToDictionary(unlock => unlock.Code);

            var achievements = AchievementCatalog.All
                .Select(code => AchievementDto.From(code, sync.Progress, unlocks.GetValueOrDefault(code)))
                .ToList();

            return new AchievementsResponseDto(achievements, unlocks.Count, AchievementCatalog.All.Count);
        }

        /// <summary>
        /// Marks the <paramref name="codes"/> the frontend showed as seen, so it stops the player for them only once, on
        /// any device. Only those: an unlock that came in meanwhile (from another tab) still gets its moment.
        /// </summary>
        public Task MarkSeenAsync(UserId userId, IReadOnlyCollection<AchievementCode> codes, CancellationToken cancellationToken)
            => codes.Count == 0
                ? Task.CompletedTask
                : _achievementRepository.MarkSeenAsync(userId, codes, _timeProvider.GetUtcNow(), cancellationToken);

        private static IReadOnlyCollection<AchievementCode> Codes(IEnumerable<AchievementUnlock> unlocks)
            => [.. unlocks.Select(unlock => unlock.Code)];
    }
}
