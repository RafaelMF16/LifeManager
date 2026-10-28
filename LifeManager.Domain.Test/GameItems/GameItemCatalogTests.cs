using LifeManager.Domain.GameItems;
using LifeManager.Domain.GameItems.Enums;
using LifeManager.Domain.GameItems.Errors;
using LifeManager.Domain.Habits;

namespace LifeManager.Domain.Test.GameItems
{
    public class GameItemCatalogTests
    {
        private static PlayerProfile Profile(int hp = GameRules.MaxHp, int coins = 1000, int streakFreezes = 0)
            => PlayerProfile.FromPersistence(1, 1, 0, hp, GameRules.MaxHp, coins, streakFreezes);

        [Theory]
        [InlineData("HpPotion", GameItemKind.HpPotion)]
        [InlineData("hppotion", GameItemKind.HpPotion)]
        [InlineData("StreakFreeze", GameItemKind.StreakFreeze)]
        public void TryParse_ShouldAcceptTheCatalogNames(string value, GameItemKind expected)
        {
            Assert.True(GameItemCatalog.TryParse(value, out var kind));
            Assert.Equal(expected, kind);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Sword")]
        [InlineData("1")]
        [InlineData("99")]
        public void TryParse_ShouldRejectAnythingElse(string value)
        {
            Assert.False(GameItemCatalog.TryParse(value, out _));
        }

        [Fact]
        public void Catalog_ShouldListBothItemsWithTheirRules()
        {
            Assert.Equal([GameItemKind.HpPotion, GameItemKind.StreakFreeze], GameItemCatalog.All);

            Assert.Equal(40, GameItemCatalog.Price(GameItemKind.HpPotion));
            Assert.Equal(3, GameItemCatalog.Limit(GameItemKind.HpPotion));
            Assert.Equal(GameItemLimitPeriod.Day, GameItemCatalog.LimitPeriod(GameItemKind.HpPotion));
            Assert.Equal(25, GameItemCatalog.HpRestore(GameItemKind.HpPotion));

            Assert.Equal(150, GameItemCatalog.Price(GameItemKind.StreakFreeze));
            Assert.Equal(1, GameItemCatalog.Limit(GameItemKind.StreakFreeze));
            Assert.Equal(GameItemLimitPeriod.Week, GameItemCatalog.LimitPeriod(GameItemKind.StreakFreeze));
            Assert.Null(GameItemCatalog.HpRestore(GameItemKind.StreakFreeze));
        }

        [Fact]
        public void PeriodStart_ShouldBeTheDayForPotionsAndTheMondayForFreezes()
        {
            var wednesday = new DateOnly(2026, 10, 28);

            Assert.Equal(wednesday, GameItemCatalog.PeriodStart(GameItemKind.HpPotion, wednesday));
            Assert.Equal(new DateOnly(2026, 10, 26), GameItemCatalog.PeriodStart(GameItemKind.StreakFreeze, wednesday));
        }

        [Fact]
        public void Purchase_ShouldChargeAndHeal_WhenBuyingAPotion()
        {
            var profile = Profile(hp: 70, coins: 100);

            var result = GameItemCatalog.Purchase(GameItemKind.HpPotion, profile, purchasedInPeriod: 0);

            Assert.True(result.IsSuccess);
            Assert.Equal(new GameDelta(-40, 0, 25), result.Value.Outcome.Applied);
            Assert.Equal(25, result.Value.HpRestored);
            Assert.Equal(40, result.Value.CostPaid);
            Assert.Equal(95, profile.Hp);
            Assert.Equal(60, profile.Coins);
        }

        [Fact]
        public void Purchase_ShouldChargeTheFullPrice_WhenThePotionHealsLess()
        {
            var profile = Profile(hp: 90, coins: 100);

            var result = GameItemCatalog.Purchase(GameItemKind.HpPotion, profile, purchasedInPeriod: 0);

            Assert.True(result.IsSuccess);
            Assert.Equal(10, result.Value.HpRestored);
            Assert.Equal(GameRules.MaxHp, profile.Hp);
            Assert.Equal(60, profile.Coins);
        }

        [Fact]
        public void Purchase_ShouldChargeAndAddAFreeze_WhenBuyingAStreakFreeze()
        {
            var profile = Profile(coins: 200, streakFreezes: 1);

            var result = GameItemCatalog.Purchase(GameItemKind.StreakFreeze, profile, purchasedInPeriod: 0);

            Assert.True(result.IsSuccess);
            Assert.Equal(new GameDelta(-150, 0, 0), result.Value.Outcome.Applied);
            Assert.Equal(0, result.Value.HpRestored);
            Assert.Equal(150, result.Value.CostPaid);
            Assert.Equal(2, profile.StreakFreezes);
            Assert.Equal(50, profile.Coins);
        }

        [Fact]
        public void Purchase_ShouldFail_WhenHpIsFull()
        {
            var profile = Profile(hp: GameRules.MaxHp);

            var result = GameItemCatalog.Purchase(GameItemKind.HpPotion, profile, purchasedInPeriod: 0);

            Assert.Equal(GameItemErrors.HpFull, result.Error);
            Assert.Equal(1000, profile.Coins);
        }

        [Fact]
        public void Purchase_ShouldFail_WhenFreezesAreFull()
        {
            var profile = Profile(streakFreezes: GameRules.MaxStreakFreezes);

            var result = GameItemCatalog.Purchase(GameItemKind.StreakFreeze, profile, purchasedInPeriod: 0);

            Assert.Equal(GameItemErrors.StreakFreezesFull, result.Error);
            Assert.Equal(GameRules.MaxStreakFreezes, profile.StreakFreezes);
            Assert.Equal(1000, profile.Coins);
        }

        [Fact]
        public void Purchase_ShouldFail_WhenTheDailyPotionLimitIsReached()
        {
            var profile = Profile(hp: 10);

            var result = GameItemCatalog.Purchase(GameItemKind.HpPotion, profile, purchasedInPeriod: 3);

            Assert.Equal(GameItemErrors.DailyLimitReached, result.Error);
            Assert.Equal(10, profile.Hp);
        }

        [Fact]
        public void Purchase_ShouldFail_WhenTheWeeklyFreezeLimitIsReached()
        {
            var result = GameItemCatalog.Purchase(GameItemKind.StreakFreeze, Profile(), purchasedInPeriod: 1);

            Assert.Equal(GameItemErrors.WeeklyLimitReached, result.Error);
        }

        [Fact]
        public void Purchase_ShouldFail_WhenCoinsAreShort()
        {
            var profile = Profile(hp: 50, coins: 39);

            var result = GameItemCatalog.Purchase(GameItemKind.HpPotion, profile, purchasedInPeriod: 0);

            Assert.Equal(GameItemErrors.InsufficientCoins, result.Error);
            Assert.Equal(50, profile.Hp);
            Assert.Equal(39, profile.Coins);
        }

        [Fact]
        public void Availability_ShouldCheckLimitThenEffectThenCoins()
        {
            var broke = Profile(hp: GameRules.MaxHp, coins: 0, streakFreezes: GameRules.MaxStreakFreezes);

            Assert.Equal(GameItemAvailability.LimitReached, GameItemCatalog.Availability(GameItemKind.HpPotion, broke, 3));
            Assert.Equal(GameItemAvailability.HpFull, GameItemCatalog.Availability(GameItemKind.HpPotion, broke, 0));
            Assert.Equal(GameItemAvailability.StreakFreezesFull, GameItemCatalog.Availability(GameItemKind.StreakFreeze, broke, 0));
            Assert.Equal(GameItemAvailability.InsufficientCoins, GameItemCatalog.Availability(GameItemKind.HpPotion, Profile(hp: 50, coins: 0), 0));
            Assert.Equal(GameItemAvailability.Available, GameItemCatalog.Availability(GameItemKind.HpPotion, Profile(hp: 50), 2));
        }

        [Fact]
        public void Undo_ShouldRefundAndTakeTheHealBack_WhenUndoingAPotion()
        {
            var profile = Profile(hp: 95, coins: 60);

            var result = GameItemCatalog.Undo(GameItemKind.HpPotion, costPaid: 40, hpRestored: 25, profile);

            Assert.True(result.IsSuccess);
            Assert.Equal(new GameDelta(40, 0, -25), result.Value.Applied);
            Assert.Equal(70, profile.Hp);
            Assert.Equal(100, profile.Coins);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(5, 1)]
        [InlineData(10, 1)]
        [InlineData(30, 5)]
        public void Undo_ShouldNeverTakeHpBelowOne(int hp, int expectedHp)
        {
            var profile = Profile(hp: hp, coins: 0);

            var result = GameItemCatalog.Undo(GameItemKind.HpPotion, costPaid: 40, hpRestored: 25, profile);

            Assert.True(result.IsSuccess);
            Assert.Equal(expectedHp, profile.Hp);
            Assert.False(result.Value.KnockedOut);
            Assert.Equal(40, profile.Coins);
        }

        [Fact]
        public void Undo_ShouldRefundAndRemoveAFreeze_WhenUndoingAStreakFreeze()
        {
            var profile = Profile(coins: 50, streakFreezes: 2);

            var result = GameItemCatalog.Undo(GameItemKind.StreakFreeze, costPaid: 150, hpRestored: 0, profile);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, profile.StreakFreezes);
            Assert.Equal(200, profile.Coins);
        }

        [Fact]
        public void Undo_ShouldFail_WhenTheFreezeWasAlreadyUsed()
        {
            var profile = Profile(coins: 50, streakFreezes: 0);

            var result = GameItemCatalog.Undo(GameItemKind.StreakFreeze, costPaid: 150, hpRestored: 0, profile);

            Assert.Equal(GameItemErrors.ItemAlreadyUsed, result.Error);
            Assert.Equal(50, profile.Coins);
        }
    }
}
