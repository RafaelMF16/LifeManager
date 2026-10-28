using LifeManager.Application.GameItems.DTOs;
using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.GameItems.Services;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Rewards.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.GameItems
{
    [Collection("ApplicationServices")]
    public class GameItemServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);

        /// <summary>A Wednesday: the freeze's week started on Monday the 26th.</summary>
        private static readonly DateOnly Today = new(2026, 10, 28);

        private readonly GameItemService _service;
        private readonly FakeTimeProvider _timeProvider;

        public GameItemServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<GameItemService>();
            _timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            _timeProvider.SetToday(Today);

            GameItemPurchaseSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            HabitProgressEntrySingleton.Instance.Clear();
            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
        }

        private static void SeedProfile(int coins, int hp = GameRules.MaxHp, int streakFreezes = 0, UserId? userId = null)
        {
            var owner = userId ?? UserId;
            PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(
                PlayerProfileSingleton.Instance.Count + 1, owner.Value, 0, hp, GameRules.MaxHp, coins, streakFreezes));
        }

        private static GameItemPurchase SeedPurchase(GameItemKind kind, DateOnly day, bool undone = false, UserId? userId = null)
        {
            var at = new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
            var purchase = GameItemPurchase.FromPersistence(
                GameItemPurchaseSingleton.Instance.Count + 1,
                (userId ?? UserId).Value,
                kind,
                GameItemCatalog.Price(kind),
                kind == GameItemKind.HpPotion ? GameRules.HpPotionRestore : 0,
                day,
                at,
                undone ? at.AddMinutes(5) : null);
            GameItemPurchaseSingleton.Instance.Add(purchase);

            return purchase;
        }

        private static PlayerProfile Profile() => PlayerProfileSingleton.Instance.Single(profile => profile.UserId == UserId);

        private Task<Result<GameItemPurchaseResultDto>> Buy(string item) => _service.PurchaseAsync(item, UserId, CancellationToken.None);

        private Task<Result<GameItemPurchaseResultDto>> Undo(int purchaseId) => _service.UndoAsync(purchaseId, UserId, CancellationToken.None);

        [Fact]
        public async Task PurchaseAsync_ShouldChargeHealAndRecordIt_WhenBuyingAPotion()
        {
            SeedProfile(coins: 100, hp: 70);

            var result = await Buy("HpPotion");

            Assert.True(result.IsSuccess);
            Assert.Equal(60, Profile().Coins);
            Assert.Equal(95, Profile().Hp);
            Assert.Equal(-40, result.Value.Wallet.CoinsDelta);
            Assert.Equal(25, result.Value.Wallet.HpDelta);
            Assert.Equal(95, result.Value.Wallet.Profile.Hp);

            var purchase = result.Value.Purchase;
            Assert.Equal(GameItemKind.HpPotion, purchase.Kind);
            Assert.Equal(40, purchase.CostPaid);
            Assert.Equal(25, purchase.HpRestored);
            Assert.Equal(Today, purchase.PurchasedOn);
            Assert.True(purchase.CanUndo);

            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.ItemPurchased, entry.Kind);
            Assert.Equal(GameItemKind.HpPotion, entry.ItemKind);
            Assert.Equal(-40, entry.CoinsDelta);
            Assert.Equal(25, entry.HpDelta);
            Assert.Null(entry.Description);
            Assert.Single(GameItemPurchaseSingleton.Instance);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldChargeTheFullPrice_WhenThePotionHealsLess()
        {
            SeedProfile(coins: 100, hp: 90);

            var result = await Buy("hppotion");

            Assert.True(result.IsSuccess);
            Assert.Equal(10, result.Value.Purchase.HpRestored);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);
            Assert.Equal(60, Profile().Coins);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldAddAFreeze_WhenBuyingAStreakFreeze()
        {
            SeedProfile(coins: 200, streakFreezes: 1);

            var result = await Buy("StreakFreeze");

            Assert.True(result.IsSuccess);
            Assert.Equal(2, Profile().StreakFreezes);
            Assert.Equal(50, Profile().Coins);
            Assert.Equal(2, result.Value.Wallet.Profile.StreakFreezes);
            Assert.Equal(0, result.Value.Purchase.HpRestored);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldStartTheProfile_WhenThePlayerHasNone()
        {
            var result = await Buy("HpPotion");

            Assert.Equal(GameItemErrors.HpFull, result.Error);
            Assert.Single(PlayerProfileSingleton.Instance);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Theory]
        [InlineData("Sword")]
        [InlineData("2")]
        [InlineData("")]
        public async Task PurchaseAsync_ShouldReturnUnknownItem_WhenTheItemIsNotInTheShop(string item)
        {
            SeedProfile(coins: 500, hp: 10);

            var result = await Buy(item);

            Assert.Equal(GameItemErrors.UnknownItem, result.Error);
            Assert.Equal(500, Profile().Coins);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldReturnHpFull_WithoutChangingAnything()
        {
            SeedProfile(coins: 500);

            var result = await Buy("HpPotion");

            Assert.Equal(GameItemErrors.HpFull, result.Error);
            AssertNothingChanged(coins: 500);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldReturnStreakFreezesFull_WithoutChangingAnything()
        {
            SeedProfile(coins: 500, streakFreezes: GameRules.MaxStreakFreezes);

            var result = await Buy("StreakFreeze");

            Assert.Equal(GameItemErrors.StreakFreezesFull, result.Error);
            AssertNothingChanged(coins: 500);
            Assert.Equal(GameRules.MaxStreakFreezes, Profile().StreakFreezes);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldReturnInsufficientCoins_WithoutChangingAnything()
        {
            SeedProfile(coins: 39, hp: 50);

            var result = await Buy("HpPotion");

            Assert.Equal(GameItemErrors.InsufficientCoins, result.Error);
            AssertNothingChanged(coins: 39);
            Assert.Equal(50, Profile().Hp);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldReturnDailyLimitReached_OnTheFourthPotionOfTheDay()
        {
            SeedProfile(coins: 500, hp: 10);

            for (var i = 0; i < GameRules.MaxHpPotionsPerDay; i++)
                Assert.True((await Buy("HpPotion")).IsSuccess);

            Profile().Apply(new GameDelta(0, 0, -50));
            var result = await Buy("HpPotion");

            Assert.Equal(GameItemErrors.DailyLimitReached, result.Error);
            Assert.Equal(GameRules.MaxHpPotionsPerDay, GameItemPurchaseSingleton.Instance.Count);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldCountOnlyTodaysPotions_ForTheDailyLimit()
        {
            SeedProfile(coins: 500, hp: 10);
            SeedPurchase(GameItemKind.HpPotion, Today.AddDays(-1));
            SeedPurchase(GameItemKind.HpPotion, Today.AddDays(-1));
            SeedPurchase(GameItemKind.HpPotion, Today.AddDays(-1));

            var result = await Buy("HpPotion");

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldReturnWeeklyLimitReached_OnTheSecondFreezeOfTheWeek()
        {
            SeedProfile(coins: 500);
            SeedPurchase(GameItemKind.StreakFreeze, new DateOnly(2026, 10, 26));

            var result = await Buy("StreakFreeze");

            Assert.Equal(GameItemErrors.WeeklyLimitReached, result.Error);
            AssertNothingChanged(coins: 500);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldAllowAFreeze_WhenLastOneWasBoughtInAnEarlierWeek()
        {
            SeedProfile(coins: 500);
            SeedPurchase(GameItemKind.StreakFreeze, new DateOnly(2026, 10, 25));

            var result = await Buy("StreakFreeze");

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task PurchaseAsync_ShouldIgnoreUndonePurchasesAndOtherUsers_ForTheLimit()
        {
            SeedProfile(coins: 500);
            SeedPurchase(GameItemKind.StreakFreeze, Today, undone: true);
            SeedPurchase(GameItemKind.StreakFreeze, Today, userId: OtherUserId);

            var result = await Buy("StreakFreeze");

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task GetAsync_ShouldListBothItemsWithThePlayersLimitsAndAvailability()
        {
            SeedProfile(coins: 100, hp: 60, streakFreezes: 0);
            SeedPurchase(GameItemKind.HpPotion, Today);
            SeedPurchase(GameItemKind.StreakFreeze, new DateOnly(2026, 10, 26));

            var response = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal([GameItemKind.HpPotion, GameItemKind.StreakFreeze], response.Items.Select(item => item.Kind));

            var potion = response.Items[0];
            Assert.Equal(40, potion.Price);
            Assert.Equal(25, potion.HpRestore);
            Assert.Equal(3, potion.Limit);
            Assert.Equal(GameItemLimitPeriod.Day, potion.LimitPeriod);
            Assert.Equal(1, potion.PurchasedInPeriod);
            Assert.Equal(GameItemAvailability.Available, potion.Availability);

            var freeze = response.Items[1];
            Assert.Equal(150, freeze.Price);
            Assert.Null(freeze.HpRestore);
            Assert.Equal(GameItemLimitPeriod.Week, freeze.LimitPeriod);
            Assert.Equal(1, freeze.PurchasedInPeriod);
            Assert.Equal(GameItemAvailability.LimitReached, freeze.Availability);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnOnlyTodaysPurchasesOfThePlayer_UndoneOnesIncluded()
        {
            SeedProfile(coins: 0, hp: 50);
            SeedPurchase(GameItemKind.HpPotion, Today.AddDays(-1));
            var undone = SeedPurchase(GameItemKind.HpPotion, Today, undone: true);
            var standing = SeedPurchase(GameItemKind.StreakFreeze, Today);
            SeedPurchase(GameItemKind.HpPotion, Today, userId: OtherUserId);

            var response = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal([undone.Id!.Value, standing.Id!.Value], response.TodayPurchases.Select(purchase => purchase.Id));
            Assert.True(response.TodayPurchases[0].IsUndone);
            Assert.False(response.TodayPurchases[0].CanUndo);
            Assert.True(response.TodayPurchases[1].CanUndo);
            Assert.Equal(GameItemAvailability.InsufficientCoins, response.Items[0].Availability);
        }

        [Fact]
        public async Task GetAsync_ShouldUseADefaultProfile_WhenThePlayerHasNone()
        {
            var response = await _service.GetAsync(UserId, CancellationToken.None);

            Assert.Equal(GameItemAvailability.HpFull, response.Items[0].Availability);
            Assert.Equal(GameItemAvailability.InsufficientCoins, response.Items[1].Availability);
            Assert.Empty(response.TodayPurchases);
            Assert.Empty(PlayerProfileSingleton.Instance);
        }

        [Fact]
        public async Task UndoAsync_ShouldRefundAndTakeTheHealBack_WhenUndoingAPotion()
        {
            SeedProfile(coins: 100, hp: 70);
            var purchase = (await Buy("HpPotion")).Value.Purchase;

            var result = await Undo(purchase.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(100, Profile().Coins);
            Assert.Equal(70, Profile().Hp);
            Assert.Equal(40, result.Value.Wallet.CoinsDelta);
            Assert.Equal(-25, result.Value.Wallet.HpDelta);
            Assert.True(result.Value.Purchase.IsUndone);
            Assert.False(result.Value.Purchase.CanUndo);
            Assert.True(GameItemPurchaseSingleton.Instance.Single().IsUndone);

            var undo = GameLedgerEntrySingleton.Instance.Last();
            Assert.Equal(GameLedgerEntryKind.Undo, undo.Kind);
            Assert.Equal(GameItemKind.HpPotion, undo.ItemKind);
            Assert.Null(undo.HabitId);
            Assert.Equal(0, GameLedgerEntrySingleton.Instance.Sum(entry => entry.CoinsDelta));
            Assert.Equal(0, GameLedgerEntrySingleton.Instance.Sum(entry => entry.HpDelta));
        }

        [Fact]
        public async Task UndoAsync_ShouldStopAtOneHp_WhenTheHealWasAlreadySpent()
        {
            SeedProfile(coins: 100, hp: 70);
            var purchase = (await Buy("HpPotion")).Value.Purchase;
            Profile().Apply(new GameDelta(0, 0, -85));

            var result = await Undo(purchase.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, Profile().Hp);
            Assert.Equal(-9, result.Value.Wallet.HpDelta);
            Assert.False(result.Value.Wallet.KnockedOut);
        }

        [Fact]
        public async Task UndoAsync_ShouldRefundAndRemoveTheFreeze_WhenUndoingAStreakFreeze()
        {
            SeedProfile(coins: 200);
            var purchase = (await Buy("StreakFreeze")).Value.Purchase;

            var result = await Undo(purchase.Id);

            Assert.True(result.IsSuccess);
            Assert.Equal(200, Profile().Coins);
            Assert.Equal(0, Profile().StreakFreezes);
        }

        [Fact]
        public async Task UndoAsync_ShouldFreeTheLimitAgain()
        {
            SeedProfile(coins: 500);
            var purchase = (await Buy("StreakFreeze")).Value.Purchase;
            Assert.Equal(GameItemErrors.WeeklyLimitReached, (await Buy("StreakFreeze")).Error);

            await Undo(purchase.Id);
            var again = await Buy("StreakFreeze");

            Assert.True(again.IsSuccess);
        }

        [Fact]
        public async Task UndoAsync_ShouldReturnItemAlreadyUsed_WhenTheDayCloseSpentTheBoughtFreeze()
        {
            // The day close judges up to the day before yesterday: Monday the 26th, missed.
            var habit = Habit.FromPersistence(
                1, UserId.Value, "Habit", null, null, HabitKind.Positive, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, null, new DateOnly(2026, 9, 1),
                new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero), null,
                currentStreak: 5, longestStreak: 5, evaluatedUntil: new DateOnly(2026, 10, 25));
            HabitSingleton.Instance.Add(habit);
            SeedProfile(coins: 200);
            var purchase = (await Buy("StreakFreeze")).Value.Purchase;

            await ServiceProvider.GetRequiredService<HabitEvaluationService>().EvaluateUserAsync(UserId, CancellationToken.None);

            var monday = HabitCheckInSingleton.Instance.Single(checkIn => checkIn.Date == new DateOnly(2026, 10, 26));
            Assert.Equal(HabitCheckInStatus.Frozen, monday.Status);
            Assert.Equal(0, Profile().StreakFreezes);
            Assert.Equal(GameRules.MaxHp, Profile().Hp);

            var result = await Undo(purchase.Id);

            Assert.Equal(GameItemErrors.ItemAlreadyUsed, result.Error);
            Assert.Equal(50, Profile().Coins);
            Assert.False(GameItemPurchaseSingleton.Instance.Single().IsUndone);
        }

        [Fact]
        public async Task UndoAsync_ShouldReturnUndoOutsideWindow_OnALaterDay()
        {
            SeedProfile(coins: 100, hp: 50);
            var purchase = SeedPurchase(GameItemKind.HpPotion, Today.AddDays(-1));

            var result = await Undo(purchase.Id!.Value);

            Assert.Equal(GameItemErrors.UndoOutsideWindow, result.Error);
            Assert.Equal(100, Profile().Coins);
        }

        [Fact]
        public async Task UndoAsync_ShouldReturnAlreadyUndone_WhenUndoneTwice()
        {
            SeedProfile(coins: 100, hp: 50);
            var purchase = (await Buy("HpPotion")).Value.Purchase;
            await Undo(purchase.Id);

            var result = await Undo(purchase.Id);

            Assert.Equal(GameItemErrors.AlreadyUndone, result.Error);
            Assert.Equal(100, Profile().Coins);
        }

        [Fact]
        public async Task UndoAsync_ShouldReturnPurchaseNotFound_ForAnotherUsersPurchase()
        {
            SeedProfile(coins: 100, hp: 50);
            var purchase = SeedPurchase(GameItemKind.HpPotion, Today, userId: OtherUserId);

            var result = await Undo(purchase.Id!.Value);

            Assert.Equal(GameItemErrors.PurchaseNotFound, result.Error);
        }

        [Fact]
        public async Task EarningPace_ShouldIgnorePurchasesAndTheirUndos()
        {
            SeedProfile(coins: 500, hp: 50);
            var outcome = new GameOutcome(new GameDelta(70, 0, 0), 0, 0, false, 0, 0);
            var earned = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Today.AddDays(-6), Today.ToDateTime(TimeOnly.MinValue), null, outcome, habitId: new HabitId(1))[0];
            earned.AssignId(1);
            GameLedgerEntrySingleton.Instance.Add(earned);
            var rewardRedemptionService = ServiceProvider.GetRequiredService<RewardRedemptionService>();
            var before = await rewardRedemptionService.GetEarningPaceAsync(UserId, CancellationToken.None);

            var potion = (await Buy("HpPotion")).Value.Purchase;
            await Buy("StreakFreeze");
            await Undo(potion.Id);
            var after = await rewardRedemptionService.GetEarningPaceAsync(UserId, CancellationToken.None);

            Assert.Equal(10m, before.AverageDailyCoins);
            Assert.Equal(before, after);
        }

        [Fact]
        public async Task Ledger_ShouldNameTheItem_OnPurchasesAndTheirUndosOnly()
        {
            SeedProfile(coins: 100, hp: 50);
            var outcome = new GameOutcome(new GameDelta(5, 0, 0), 0, 0, false, 0, 0);
            var habitUndo = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.Undo, Today, Today.ToDateTime(TimeOnly.MinValue), "Read", outcome, habitId: new HabitId(1))[0];
            habitUndo.AssignId(1);
            GameLedgerEntrySingleton.Instance.Add(habitUndo);
            var potion = (await Buy("HpPotion")).Value.Purchase;
            await Undo(potion.Id);

            var ledger = await ServiceProvider.GetRequiredService<GameLedgerService>()
                .GetPagedAsync(new GameLedgerListQueryDto(), UserId, CancellationToken.None);

            var byKind = ledger.Value!.Items.ToDictionary(entry => entry.Id);
            Assert.Null(byKind[1].ItemKind);
            Assert.All(ledger.Value.Items.Where(entry => entry.Id != 1), entry => Assert.Equal(GameItemKind.HpPotion, entry.ItemKind));
            Assert.Equal(3, ledger.Value.Items.Count);
        }

        private static void AssertNothingChanged(int coins)
        {
            Assert.Equal(coins, Profile().Coins);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
            Assert.DoesNotContain(GameItemPurchaseSingleton.Instance, purchase => purchase.PurchasedOn == Today && !purchase.IsUndone && purchase.UserId == UserId);
        }
    }
}
