using LifeManager.Application.GameItems.DTOs;
using LifeManager.Application.GameItems.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
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

        private static void AssertNothingChanged(int coins)
        {
            Assert.Equal(coins, Profile().Coins);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
            Assert.DoesNotContain(GameItemPurchaseSingleton.Instance, purchase => purchase.PurchasedOn == Today && !purchase.IsUndone && purchase.UserId == UserId);
        }
    }
}
