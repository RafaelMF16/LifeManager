using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Habits
{
    public class GameLedgerEntryTests
    {
        private static readonly UserId UserId = new(1);
        private static readonly DateOnly Day = new(2026, 10, 7);
        private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

        private static PlayerProfile Profile(int totalXp = 0, int hp = GameRules.MaxHp, int coins = 0)
            => PlayerProfile.FromPersistence(1, UserId.Value, totalXp, hp, GameRules.MaxHp, coins, 0);

        [Fact]
        public void FromOutcome_ShouldReturnOneEntryWithTheAppliedDelta_WhenNothingElseHappened()
        {
            var outcome = Profile(hp: 99).Apply(new GameDelta(Coins: 5, Xp: 10, Hp: 5));

            var entries = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Day, Now, "  Ler 10 páginas ", outcome);

            var entry = Assert.Single(entries);
            Assert.Equal(GameLedgerEntryKind.HabitDone, entry.Kind);
            Assert.Equal(UserId, entry.UserId);
            Assert.Equal(Day, entry.OccurredOn);
            Assert.Equal(Now, entry.CreatedAt);
            Assert.Equal(5, entry.CoinsDelta);
            Assert.Equal(10, entry.XpDelta);
            Assert.Equal(1, entry.HpDelta);
            Assert.Equal("Ler 10 páginas", entry.Description);
        }

        [Fact]
        public void FromOutcome_ShouldAddALevelUpEntryWithTheRestoredHp_WhenThePlayerLeveledUp()
        {
            var outcome = Profile(totalXp: 95, hp: 40).Apply(new GameDelta(Coins: 5, Xp: 10, Hp: 1));

            var entries = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Day, Now, "Ler", outcome);

            Assert.Equal(2, entries.Count);
            Assert.Equal(GameLedgerEntryKind.HabitDone, entries[0].Kind);
            var levelUp = entries[1];
            Assert.Equal(GameLedgerEntryKind.LevelUp, levelUp.Kind);
            Assert.Equal(59, levelUp.HpDelta);
            Assert.Equal(0, levelUp.CoinsDelta);
            Assert.Equal(0, levelUp.XpDelta);
            Assert.Null(levelUp.Description);
        }

        [Fact]
        public void FromOutcome_ShouldAddAKnockoutEntryWithTheLostCoinsAndRestoredHp_WhenThePlayerWasKnockedOut()
        {
            var outcome = Profile(hp: 5, coins: 50).Apply(new GameDelta(Coins: 0, Xp: 0, Hp: -8));

            var entries = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitMissed, Day, Now, "Treinar", outcome);

            Assert.Equal(2, entries.Count);
            Assert.Equal(GameLedgerEntryKind.HabitMissed, entries[0].Kind);
            Assert.Equal(-5, entries[0].HpDelta);
            var knockout = entries[1];
            Assert.Equal(GameLedgerEntryKind.Knockout, knockout.Kind);
            Assert.Equal(-10, knockout.CoinsDelta);
            Assert.Equal(GameRules.MaxHp, knockout.HpDelta);
        }

        [Fact]
        public void FromOutcome_ShouldReturnThreeEntries_WhenTheDeltaLevelsUpAndKnocksOut()
        {
            // Apply never produces both (a level-up fills the HP), but the order must still hold: main entry, level-up, knockout.
            var outcome = new GameOutcome(new GameDelta(0, 10, -10), LevelsGained: 1, LevelUpHpRestored: 0, KnockedOut: true, KnockoutCoinsLost: 2, KnockoutHpRestored: 100);

            var entries = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Day, Now, null, outcome);

            Assert.Equal(
                [GameLedgerEntryKind.HabitDone, GameLedgerEntryKind.LevelUp, GameLedgerEntryKind.Knockout],
                entries.Select(entry => entry.Kind));
        }

        [Fact]
        public void FromOutcome_ShouldSumToTheProfileChange_WhenSeveralEffectsHappen()
        {
            var profile = Profile(totalXp: 290, hp: 70, coins: 33);
            var (startXp, startHp, startCoins) = (profile.TotalXp, profile.Hp, profile.Coins);
            var allEntries = new List<GameLedgerEntry>();

            foreach (var delta in new[] { new GameDelta(20, 40, 1), new GameDelta(0, 0, -60), new GameDelta(0, 0, -45), new GameDelta(-5, -40, -1) })
            {
                var outcome = profile.Apply(delta);
                allEntries.AddRange(GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Day, Now, null, outcome));
            }

            Assert.Equal(profile.Coins - startCoins, allEntries.Sum(entry => entry.CoinsDelta));
            Assert.Equal(profile.TotalXp - startXp, allEntries.Sum(entry => entry.XpDelta));
            Assert.Equal(profile.Hp - startHp, allEntries.Sum(entry => entry.HpDelta));
        }

        [Fact]
        public void FromOutcome_ShouldTruncateTheDescription_WhenItIsTooLong()
        {
            var outcome = Profile().Apply(GameDelta.None);

            var entry = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Day, Now, new string('a', 100), outcome).Single();

            Assert.Equal(GameLedgerEntry.DescriptionMaxLength, entry.Description!.Length);
        }

        [Fact]
        public void FromOutcome_ShouldStoreNoDescription_WhenItIsBlank()
        {
            var outcome = Profile().Apply(GameDelta.None);

            var entry = GameLedgerEntry.FromOutcome(UserId, GameLedgerEntryKind.HabitDone, Day, Now, "   ", outcome).Single();

            Assert.Null(entry.Description);
        }
    }
}
