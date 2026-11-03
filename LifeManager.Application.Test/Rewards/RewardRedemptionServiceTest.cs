using LifeManager.Application.Rewards.DTOs;
using LifeManager.Application.Rewards.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Rewards
{
    [Collection("ApplicationServices")]
    public class RewardRedemptionServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 8);

        private readonly RewardRedemptionService _service;
        private readonly FakeTimeProvider _timeProvider;

        public RewardRedemptionServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<RewardRedemptionService>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Today);

            RewardSingleton.Instance.Clear();
            RewardRedemptionSingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
        }

        private const int LeisureCategoryId = 1;

        private static Reward SeedRewardWithExpense(int cost = 50, decimal expenseAmount = 120m)
        {
            var category = Category.Create(UserId.Value, "Lazer").Value!;
            category.AssignId(LeisureCategoryId);
            CategorySingleton.Instance.Add(category);

            var reward = Reward.FromPersistence(
                RewardSingleton.Instance.Count + 1,
                UserId.Value,
                "Dinner out",
                cost,
                null,
                new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
                null,
                LeisureCategoryId,
                expenseAmount);
            RewardSingleton.Instance.Add(reward);

            return reward;
        }

        private static MonthlySummary SeedOctober()
        {
            var month = MonthlySummary.FromPersistence(7, UserId.Value, 10, 2026, 0, 0, 0);
            MonthlySummarySingleton.Instance.Add(month);

            return month;
        }

        private Task<Domain.Shared.Results.Result<RewardRedemptionResultDto>> RedeemWithExpense(Reward reward)
            => _service.RedeemAsync(reward.Id!.Value, new RewardRedeemDto(RegisterExpense: true), UserId, CancellationToken.None);

        private static Reward SeedReward(int cost = 50, bool archived = false, UserId? userId = null)
        {
            var reward = Reward.FromPersistence(
                RewardSingleton.Instance.Count + 1,
                (userId ?? UserId).Value,
                "Video games",
                cost,
                "gamepad-2",
                new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
                archived ? new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) : null);
            RewardSingleton.Instance.Add(reward);

            return reward;
        }

        private static void SeedProfile(int coins, int hp = GameRules.MaxHp)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, 0, hp, GameRules.MaxHp, coins, 0));

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private static void SeedLedger(GameLedgerEntryKind kind, int coins, DateOnly day, HabitId? habitId = null, RewardId? rewardId = null)
        {
            var outcome = new GameOutcome(new GameDelta(coins, 0, 0), 0, 0, false, 0, 0);
            var entry = GameLedgerEntry.FromOutcome(UserId, kind, day, day.ToDateTime(TimeOnly.MinValue), null, outcome, habitId, rewardId)[0];
            entry.AssignId(GameLedgerEntrySingleton.Instance.Count + 1);
            GameLedgerEntrySingleton.Instance.Add(entry);
        }

        private Task<Domain.Shared.Results.Result<RewardRedemptionResultDto>> Redeem(Reward reward)
            => _service.RedeemAsync(reward.Id!.Value, null, UserId, CancellationToken.None);

        [Fact]
        public async Task RedeemAsync_ShouldPostTheExpenseIntoTodaysMonth_WhenRegisterExpense()
        {
            SeedProfile(coins: 80);
            SeedOctober();
            var reward = SeedRewardWithExpense(cost: 50, expenseAmount: 120m);

            var result = await RedeemWithExpense(reward);

            Assert.True(result.IsSuccess);
            var expense = Assert.Single(TransactionSingleton.Instance);
            Assert.Equal(MoneyFlowType.Expense, expense.Type);
            Assert.Equal(120m, expense.Amount.Value);
            Assert.Equal("Dinner out", expense.Description.Value);
            Assert.Equal(Today, expense.TransactionDate);
            Assert.Equal(new CategoryId(LeisureCategoryId), expense.CategoryId);
            Assert.Equal(expense.Id!.Value, result.Value.Redemption.TransactionId);
            Assert.Equal(7, result.Value.MonthlySummaryId);
            Assert.Equal(expense.Id, RewardRedemptionSingleton.Instance.Single().TransactionId);
            Assert.Equal(120m, MonthlySummarySingleton.Instance.Single().TotalExpense.Value);
            Assert.Equal(30, Profile().Coins);
        }

        [Fact]
        public async Task RedeemAsync_ShouldOpenTodaysMonth_WhenItIsMissing()
        {
            SeedProfile(coins: 80);
            var reward = SeedRewardWithExpense();

            var result = await RedeemWithExpense(reward);

            Assert.True(result.IsSuccess);
            var month = Assert.Single(MonthlySummarySingleton.Instance);
            Assert.Equal((2026, 10), (month.Year.Value, month.Month.Value));
            Assert.Equal(month.Id!.Value, result.Value.MonthlySummaryId);
            Assert.Equal(120m, month.TotalExpense.Value);
        }

        [Fact]
        public async Task RedeemAsync_ShouldNotPostTheExpense_WithoutRegisterExpense()
        {
            SeedProfile(coins: 80);
            var reward = SeedRewardWithExpense();

            var result = await Redeem(reward);

            Assert.True(result.IsSuccess);
            Assert.Empty(TransactionSingleton.Instance);
            Assert.Null(result.Value.Redemption.TransactionId);
            Assert.Null(result.Value.MonthlySummaryId);
        }

        [Fact]
        public async Task RedeemAsync_ShouldReturnExpenseNotConfigured_WhenTheRewardHasNoExpense()
        {
            SeedProfile(coins: 80);
            var reward = SeedReward(cost: 50);

            var result = await _service.RedeemAsync(reward.Id!.Value, new RewardRedeemDto(RegisterExpense: true), UserId, CancellationToken.None);

            Assert.Equal(RewardErrors.ExpenseNotConfigured, result.Error);
            Assert.Equal(80, Profile().Coins);
            Assert.Empty(RewardRedemptionSingleton.Instance);
        }

        [Fact]
        public async Task RedeemAsync_ShouldPostNothing_WhenCoinsAreShort()
        {
            SeedProfile(coins: 10);
            SeedOctober();
            var reward = SeedRewardWithExpense(cost: 50);

            var result = await RedeemWithExpense(reward);

            Assert.Equal(RewardErrors.InsufficientCoins, result.Error);
            Assert.Empty(TransactionSingleton.Instance);
            Assert.Equal(0m, MonthlySummarySingleton.Instance.Single().TotalExpense.Value);
        }

        [Fact]
        public async Task RedeemAsync_ShouldSpendTheCoinsAndRecordIt()
        {
            SeedProfile(coins: 80);
            var reward = SeedReward(cost: 50);

            var result = await Redeem(reward);

            Assert.True(result.IsSuccess);
            Assert.Equal(30, Profile().Coins);
            Assert.Equal(-50, result.Value.Wallet.CoinsDelta);
            Assert.Equal(30, result.Value.Wallet.Profile.Coins);

            var redemption = Assert.Single(RewardRedemptionSingleton.Instance);
            Assert.Equal(50, redemption.CostPaid);
            Assert.Equal(Today, redemption.RedeemedOn);
            Assert.Equal("Video games", redemption.RewardName);
            Assert.True(result.Value.Redemption.CanUndo);

            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.RewardRedeemed, entry.Kind);
            Assert.Equal(-50, entry.CoinsDelta);
            Assert.Equal(reward.Id, entry.RewardId);
            Assert.Null(entry.HabitId);
            Assert.Equal("Video games", entry.Description);
        }

        [Fact]
        public async Task RedeemAsync_ShouldAllowSpendingTheWholeBalance()
        {
            SeedProfile(coins: 50);

            var result = await Redeem(SeedReward(cost: 50));

            Assert.True(result.IsSuccess);
            Assert.Equal(0, Profile().Coins);
        }

        [Fact]
        public async Task RedeemAsync_ShouldFailWithoutWritingAnything_WhenCoinsAreShort()
        {
            SeedProfile(coins: 49);

            var result = await Redeem(SeedReward(cost: 50));

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.InsufficientCoins, result.Error);
            Assert.Equal(49, Profile().Coins);
            Assert.Empty(RewardRedemptionSingleton.Instance);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task RedeemAsync_ShouldFail_ForANewPlayerWithNoCoins()
        {
            var result = await Redeem(SeedReward(cost: 1));

            Assert.Equal(RewardErrors.InsufficientCoins, result.Error);
            Assert.Empty(RewardRedemptionSingleton.Instance);
        }

        [Fact]
        public async Task RedeemAsync_ShouldFail_WhenTheRewardIsArchived()
        {
            SeedProfile(coins: 100);

            var result = await Redeem(SeedReward(archived: true));

            Assert.Equal(RewardErrors.Archived, result.Error);
            Assert.Equal(100, Profile().Coins);
        }

        [Fact]
        public async Task RedeemAsync_ShouldReturnNotFound_ForAnotherUsersReward()
        {
            SeedProfile(coins: 100);

            var result = await Redeem(SeedReward(userId: OtherUserId));

            Assert.Equal(RewardErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task RedeemAsync_ShouldNotChangeHpOrXp()
        {
            SeedProfile(coins: 100, hp: 40);

            await Redeem(SeedReward(cost: 50));

            Assert.Equal(40, Profile().Hp);
            Assert.Equal(0, Profile().TotalXp);
        }

        [Fact]
        public async Task UndoAsync_ShouldGiveTheCoinsBack_OnTheSameDay()
        {
            SeedProfile(coins: 80);
            var redeemed = await Redeem(SeedReward(cost: 50));

            var result = await _service.UndoAsync(redeemed.Value.Redemption.Id, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(80, Profile().Coins);
            Assert.Equal(50, result.Value.Wallet.CoinsDelta);
            Assert.NotNull(result.Value.Redemption.UndoneAt);
            Assert.False(result.Value.Redemption.CanUndo);
            Assert.True(RewardRedemptionSingleton.Instance.Single().IsUndone);

            var undo = GameLedgerEntrySingleton.Instance.Last();
            Assert.Equal(GameLedgerEntryKind.Undo, undo.Kind);
            Assert.Equal(50, undo.CoinsDelta);
            Assert.Equal(new RewardId(1), undo.RewardId);
        }

        [Fact]
        public async Task UndoAsync_ShouldRefundThePricePaid_EvenIfTheRewardGotPricier()
        {
            SeedProfile(coins: 80);
            var reward = SeedReward(cost: 50);
            var redeemed = await Redeem(reward);
            RewardSingleton.Instance[0] = Reward.FromPersistence(reward.Id!.Value, UserId.Value, "Video games", 70, null, reward.CreatedAt, null);

            await _service.UndoAsync(redeemed.Value.Redemption.Id, UserId, CancellationToken.None);

            Assert.Equal(80, Profile().Coins);
        }

        [Fact]
        public async Task UndoAsync_ShouldFail_OnALaterDay()
        {
            SeedProfile(coins: 80);
            var redeemed = await Redeem(SeedReward(cost: 50));
            _timeProvider.SetToday(Today.AddDays(1));

            var result = await _service.UndoAsync(redeemed.Value.Redemption.Id, UserId, CancellationToken.None);

            Assert.Equal(RewardErrors.UndoOutsideWindow, result.Error);
            Assert.Equal(30, Profile().Coins);
        }

        [Fact]
        public async Task UndoAsync_ShouldFail_TheSecondTime()
        {
            SeedProfile(coins: 80);
            var redeemed = await Redeem(SeedReward(cost: 50));
            await _service.UndoAsync(redeemed.Value.Redemption.Id, UserId, CancellationToken.None);

            var result = await _service.UndoAsync(redeemed.Value.Redemption.Id, UserId, CancellationToken.None);

            Assert.Equal(RewardErrors.AlreadyUndone, result.Error);
            Assert.Equal(80, Profile().Coins);
        }

        [Fact]
        public async Task UndoAsync_ShouldReturnNotFound_ForAnotherUsersRedemption()
        {
            SeedProfile(coins: 80);
            var redeemed = await Redeem(SeedReward(cost: 50));

            var result = await _service.UndoAsync(redeemed.Value.Redemption.Id, OtherUserId, CancellationToken.None);

            Assert.Equal(RewardErrors.RedemptionNotFound, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldListNewestFirst_AndOnlyLetTodaysBeUndone()
        {
            SeedProfile(coins: 200);
            var reward = SeedReward(cost: 10);

            _timeProvider.SetToday(Today.AddDays(-1));
            await Redeem(reward);
            _timeProvider.SetToday(Today);
            await Redeem(reward);

            var result = await _service.GetPagedAsync(new RewardRedemptionListQueryDto(), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal([Today, Today.AddDays(-1)], result.Value.Items.Select(item => item.RedeemedOn));
            Assert.Equal([true, false], result.Value.Items.Select(item => item.CanUndo));
        }

        [Fact]
        public async Task GetEarningPaceAsync_ShouldCountHabitEarningsOnly()
        {
            var habitId = new HabitId(3);
            SeedLedger(GameLedgerEntryKind.HabitDone, 20, Today.AddDays(-1), habitId);
            SeedLedger(GameLedgerEntryKind.CleanDay, 10, Today, habitId);
            SeedLedger(GameLedgerEntryKind.StreakMilestone, 25, Today, habitId);
            SeedLedger(GameLedgerEntryKind.Undo, -5, Today, habitId);
            SeedLedger(GameLedgerEntryKind.RewardRedeemed, -50, Today, rewardId: new RewardId(1));
            SeedLedger(GameLedgerEntryKind.Undo, 50, Today, rewardId: new RewardId(1));
            SeedLedger(GameLedgerEntryKind.Knockout, -30, Today, habitId);
            // Outside the window.
            SeedLedger(GameLedgerEntryKind.HabitDone, 1000, Today.AddDays(-RewardPace.WindowDays), habitId);

            var pace = await _service.GetEarningPaceAsync(UserId, CancellationToken.None);

            // 50 coins since yesterday: 2 days.
            Assert.Equal(25m, pace.AverageDailyCoins);
            Assert.Equal(RewardPace.WindowDays, pace.WindowDays);
        }

        [Fact]
        public async Task GetEarningPaceAsync_ShouldBeZero_WithoutEarnings()
        {
            var pace = await _service.GetEarningPaceAsync(UserId, CancellationToken.None);

            Assert.Equal(0m, pace.AverageDailyCoins);
        }
    }
}
