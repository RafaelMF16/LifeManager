using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class PlayerProfileTests
    {
        private static PlayerProfile Profile(int totalXp = 0, int hp = GameRules.MaxHp, int coins = 0, int streakFreezes = 0)
            => PlayerProfile.FromPersistence(1, 1, totalXp, hp, GameRules.MaxHp, coins, streakFreezes);

        [Fact]
        public void CreateDefault_ShouldStartAtLevelOneWithFullHpAndNoCoins()
        {
            var profile = PlayerProfile.CreateDefault(new UserId(7));

            Assert.Null(profile.Id);
            Assert.Equal(7, profile.UserId.Value);
            Assert.Equal(1, profile.Level);
            Assert.Equal(0, profile.TotalXp);
            Assert.Equal(GameRules.MaxHp, profile.Hp);
            Assert.Equal(GameRules.MaxHp, profile.MaxHp);
            Assert.Equal(0, profile.Coins);
            Assert.Equal(0, profile.StreakFreezes);
            Assert.Equal(0, profile.XpInLevel);
            Assert.Equal(100, profile.XpToNextLevel);
        }

        [Fact]
        public void FromPersistence_ShouldDeriveLevelAndXpInLevel_FromTotalXp()
        {
            var profile = Profile(totalXp: 340);

            Assert.Equal(3, profile.Level);
            Assert.Equal(40, profile.XpInLevel);
            Assert.Equal(300, profile.XpToNextLevel);
        }

        [Fact]
        public void Apply_ShouldAddCoinsXpAndHp_WhenNothingHitsALimit()
        {
            var profile = Profile(totalXp: 10, hp: 50, coins: 20);

            var outcome = profile.Apply(new GameDelta(Coins: 10, Xp: 20, Hp: 1));

            Assert.Equal(new GameDelta(10, 20, 1), outcome.Applied);
            Assert.Equal(30, profile.Coins);
            Assert.Equal(30, profile.TotalXp);
            Assert.Equal(51, profile.Hp);
            Assert.False(outcome.LeveledUp);
            Assert.False(outcome.KnockedOut);
        }

        [Fact]
        public void Apply_ShouldLevelUpAndFillHp_WhenXpReachesTheNextLevel()
        {
            var profile = Profile(totalXp: 95, hp: 40);

            var outcome = profile.Apply(new GameDelta(Coins: 5, Xp: 10, Hp: 1));

            Assert.Equal(2, profile.Level);
            Assert.Equal(5, profile.XpInLevel);
            Assert.Equal(GameRules.MaxHp, profile.Hp);
            Assert.Equal(1, outcome.LevelsGained);
            Assert.Equal(1, outcome.Applied.Hp);
            Assert.Equal(GameRules.MaxHp - 41, outcome.LevelUpHpRestored);
        }

        [Fact]
        public void Apply_ShouldGainSeveralLevels_WhenXpCrossesSeveralThresholds()
        {
            var profile = Profile(totalXp: 0);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: 650, Hp: 0));

            Assert.Equal(4, profile.Level);
            Assert.Equal(3, outcome.LevelsGained);
            Assert.Equal(50, profile.XpInLevel);
        }

        [Fact]
        public void Apply_ShouldCapHpAtMaxAndRecordOnlyWhatFit_WhenHealingAtNearlyFullHp()
        {
            var profile = Profile(hp: 99);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: 0, Hp: 5));

            Assert.Equal(GameRules.MaxHp, profile.Hp);
            Assert.Equal(1, outcome.Applied.Hp);
        }

        [Fact]
        public void Apply_ShouldKnockOut_WhenHpReachesZero()
        {
            var profile = Profile(totalXp: 340, hp: 8, coins: 99);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: 0, Hp: -8));

            Assert.True(outcome.KnockedOut);
            Assert.Equal(-8, outcome.Applied.Hp);
            Assert.Equal(19, outcome.KnockoutCoinsLost);
            Assert.Equal(GameRules.MaxHp, outcome.KnockoutHpRestored);
            Assert.Equal(80, profile.Coins);
            Assert.Equal(GameRules.MaxHp, profile.Hp);
            Assert.Equal(3, profile.Level);
            Assert.Equal(340, profile.TotalXp);
        }

        [Fact]
        public void Apply_ShouldKnockOutAndRecordOnlyTheRemainingHp_WhenDamageIsGreaterThanHp()
        {
            var profile = Profile(hp: 3, coins: 50);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: 0, Hp: -12));

            Assert.True(outcome.KnockedOut);
            Assert.Equal(-3, outcome.Applied.Hp);
            Assert.Equal(10, outcome.KnockoutCoinsLost);
            Assert.Equal(40, profile.Coins);
            Assert.Equal(GameRules.MaxHp, profile.Hp);
        }

        [Fact]
        public void Apply_ShouldNotKnockOut_WhenDamageLeavesSomeHp()
        {
            var profile = Profile(hp: 9, coins: 50);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: 0, Hp: -8));

            Assert.False(outcome.KnockedOut);
            Assert.Equal(1, profile.Hp);
            Assert.Equal(50, profile.Coins);
        }

        [Fact]
        public void Apply_ShouldNeverLeaveCoinsNegative_WhenLosingMoreThanTheBalance()
        {
            var profile = Profile(coins: 7);

            var outcome = profile.Apply(new GameDelta(Coins: -10, Xp: 0, Hp: 0));

            Assert.Equal(0, profile.Coins);
            Assert.Equal(-7, outcome.Applied.Coins);
        }

        [Fact]
        public void Apply_ShouldLowerTheLevelWithoutTouchingHp_WhenXpIsTakenBack()
        {
            var profile = Profile(totalXp: 105, hp: 60);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: -10, Hp: 0));

            Assert.Equal(1, profile.Level);
            Assert.Equal(95, profile.TotalXp);
            Assert.Equal(60, profile.Hp);
            Assert.Equal(0, outcome.LevelsGained);
        }

        [Fact]
        public void Apply_ShouldNeverLeaveXpNegative_WhenTakingBackMoreThanTheTotal()
        {
            var profile = Profile(totalXp: 5);

            var outcome = profile.Apply(new GameDelta(Coins: 0, Xp: -20, Hp: 0));

            Assert.Equal(0, profile.TotalXp);
            Assert.Equal(-5, outcome.Applied.Xp);
        }

        [Fact]
        public void Apply_ShouldBeUndoneByItsAppliedInverse_WhenNoLevelUpOrKnockoutHappened()
        {
            var profile = Profile(totalXp: 10, hp: 99, coins: 3);
            var outcome = profile.Apply(new GameDelta(Coins: 10, Xp: 20, Hp: 5));

            profile.Apply(outcome.Applied.Inverse());

            Assert.Equal(10, profile.TotalXp);
            Assert.Equal(99, profile.Hp);
            Assert.Equal(3, profile.Coins);
        }

        [Fact]
        public void AddStreakFreeze_ShouldAddOne_WhenBelowTheMax()
        {
            var profile = Profile(streakFreezes: 1);

            var result = profile.AddStreakFreeze();

            Assert.True(result.IsSuccess);
            Assert.Equal(2, profile.StreakFreezes);
        }

        [Fact]
        public void AddStreakFreeze_ShouldReturnError_WhenAtTheMax()
        {
            var profile = Profile(streakFreezes: GameRules.MaxStreakFreezes);

            var result = profile.AddStreakFreeze();

            Assert.False(result.IsSuccess);
            Assert.Equal(PlayerProfileErrors.StreakFreezesFull, result.Error);
            Assert.Equal(GameRules.MaxStreakFreezes, profile.StreakFreezes);
        }

        [Fact]
        public void UseStreakFreeze_ShouldUseOne_WhenThePlayerHasAny()
        {
            var profile = Profile(streakFreezes: 2);

            var result = profile.UseStreakFreeze();

            Assert.True(result.IsSuccess);
            Assert.Equal(1, profile.StreakFreezes);
        }

        [Fact]
        public void UseStreakFreeze_ShouldReturnError_WhenThePlayerHasNone()
        {
            var profile = Profile(streakFreezes: 0);

            var result = profile.UseStreakFreeze();

            Assert.False(result.IsSuccess);
            Assert.Equal(PlayerProfileErrors.NoStreakFreeze, result.Error);
            Assert.Equal(0, profile.StreakFreezes);
        }
    }
}
