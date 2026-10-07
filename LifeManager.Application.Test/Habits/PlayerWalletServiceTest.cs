using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class PlayerWalletServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly DateOnly Today = new(2026, 10, 7);

        private readonly PlayerWalletService _playerWalletService;

        public PlayerWalletServiceTest()
        {
            _playerWalletService = ServiceProvider.GetRequiredService<PlayerWalletService>();

            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();

            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);
        }

        private Task<WalletChangeDto> ApplyAsync(GameLedgerEntryKind kind, GameDelta delta, UserId? userId = null, string? description = null)
            => _playerWalletService.ApplyAsync(userId ?? UserId, kind, delta, Today, description, CancellationToken.None);

        private static void SeedProfile(int totalXp = 0, int hp = GameRules.MaxHp, int coins = 0)
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(PlayerProfileSingleton.Instance.Count + 1, UserId.Value, totalXp, hp, GameRules.MaxHp, coins, 0));

        [Fact]
        public async Task GetProfileAsync_ShouldReturnANewPlayerWithoutSavingIt_WhenTheUserHasNoProfile()
        {
            var profile = await _playerWalletService.GetProfileAsync(UserId, CancellationToken.None);

            Assert.Equal(1, profile.Level);
            Assert.Equal(0, profile.XpInLevel);
            Assert.Equal(100, profile.XpToNextLevel);
            Assert.Equal(0, profile.TotalXp);
            Assert.Equal(GameRules.MaxHp, profile.Hp);
            Assert.Equal(GameRules.MaxHp, profile.MaxHp);
            Assert.Equal(0, profile.Coins);
            Assert.Equal(0, profile.StreakFreezes);
            Assert.Equal(GameRules.MaxStreakFreezes, profile.MaxStreakFreezes);
            Assert.Empty(PlayerProfileSingleton.Instance);
        }

        [Fact]
        public async Task GetProfileAsync_ShouldReturnTheStoredProfile_WhenTheUserHasOne()
        {
            SeedProfile(totalXp: 340, hp: 72, coins: 45);

            var profile = await _playerWalletService.GetProfileAsync(UserId, CancellationToken.None);

            Assert.Equal(3, profile.Level);
            Assert.Equal(40, profile.XpInLevel);
            Assert.Equal(300, profile.XpToNextLevel);
            Assert.Equal(72, profile.Hp);
            Assert.Equal(45, profile.Coins);
        }

        [Fact]
        public async Task ApplyAsync_ShouldCreateTheProfile_WhenItIsTheUsersFirstChange()
        {
            var change = await ApplyAsync(GameLedgerEntryKind.HabitDone, new GameDelta(5, 10, 1), description: "Ler");

            var storedProfile = Assert.Single(PlayerProfileSingleton.Instance);
            Assert.Equal(UserId, storedProfile.UserId);
            Assert.Equal(5, storedProfile.Coins);
            Assert.Equal(10, storedProfile.TotalXp);
            Assert.Equal(GameRules.MaxHp, storedProfile.Hp);
            Assert.Equal(5, change.Profile.Coins);
            Assert.Equal(5, change.CoinsDelta);
            Assert.Equal(10, change.XpDelta);
            Assert.Equal(0, change.HpDelta);

            var entry = Assert.Single(GameLedgerEntrySingleton.Instance);
            Assert.Equal(GameLedgerEntryKind.HabitDone, entry.Kind);
            Assert.Equal(Today, entry.OccurredOn);
            Assert.Equal("Ler", entry.Description);
        }

        [Fact]
        public async Task ApplyAsync_ShouldOnlyChangeTheGivenUsersProfile_WhenOtherUsersHaveProfiles()
        {
            await ApplyAsync(GameLedgerEntryKind.HabitDone, new GameDelta(5, 10, 0), new UserId(2));

            await ApplyAsync(GameLedgerEntryKind.HabitDone, new GameDelta(20, 40, 0));

            Assert.Equal(2, PlayerProfileSingleton.Instance.Count);
            Assert.Equal(5, PlayerProfileSingleton.Instance.Single(profile => profile.UserId.Value == 2).Coins);
            Assert.Equal(20, PlayerProfileSingleton.Instance.Single(profile => profile.UserId.Value == 1).Coins);
        }

        [Fact]
        public async Task ApplyAsync_ShouldRecordHabitDoneAndLevelUp_WhenTheCheckInLevelsUp()
        {
            SeedProfile(totalXp: 95, hp: 40);

            var change = await ApplyAsync(GameLedgerEntryKind.HabitDone, new GameDelta(5, 10, 1));

            Assert.Equal(1, change.LevelsGained);
            Assert.Equal(2, change.Profile.Level);
            Assert.Equal(GameRules.MaxHp, change.Profile.Hp);
            Assert.Equal(
                [GameLedgerEntryKind.HabitDone, GameLedgerEntryKind.LevelUp],
                GameLedgerEntrySingleton.Instance.Select(entry => entry.Kind));
            Assert.Equal(59, GameLedgerEntrySingleton.Instance[1].HpDelta);
        }

        [Fact]
        public async Task ApplyAsync_ShouldRecordHabitMissedAndKnockout_WhenTheDamageKnocksOut()
        {
            SeedProfile(totalXp: 340, hp: 8, coins: 99);

            var change = await ApplyAsync(GameLedgerEntryKind.HabitMissed, new GameDelta(0, 0, -12));

            Assert.True(change.KnockedOut);
            Assert.Equal(19, change.KnockoutCoinsLost);
            Assert.Equal(-8, change.HpDelta);
            Assert.Equal(80, change.Profile.Coins);
            Assert.Equal(GameRules.MaxHp, change.Profile.Hp);
            Assert.Equal(3, change.Profile.Level);
            Assert.Equal(
                [GameLedgerEntryKind.HabitMissed, GameLedgerEntryKind.Knockout],
                GameLedgerEntrySingleton.Instance.Select(entry => entry.Kind));
            Assert.Equal(-19, GameLedgerEntrySingleton.Instance[1].CoinsDelta);

            var storedProfile = Assert.Single(PlayerProfileSingleton.Instance);
            Assert.Equal(80, storedProfile.Coins);
            Assert.Equal(GameRules.MaxHp, storedProfile.Hp);
        }

        [Fact]
        public async Task ApplyAsync_ShouldKeepTheLedgerEqualToTheProfile_AfterSeveralChanges()
        {
            var deltas = new[]
            {
                (GameLedgerEntryKind.HabitDone, new GameDelta(20, 40, 1)),
                (GameLedgerEntryKind.HabitDone, new GameDelta(20, 70, 1)),
                (GameLedgerEntryKind.HabitMissed, new GameDelta(0, 0, -60)),
                (GameLedgerEntryKind.HabitMissed, new GameDelta(0, 0, -45)),
                (GameLedgerEntryKind.Undo, new GameDelta(-20, -70, -1)),
            };

            foreach (var (kind, delta) in deltas)
                await ApplyAsync(kind, delta);

            var profile = Assert.Single(PlayerProfileSingleton.Instance);
            var start = PlayerProfile.CreateDefault(UserId);
            var ledger = GameLedgerEntrySingleton.Instance;
            Assert.Equal(profile.Coins - start.Coins, ledger.Sum(entry => entry.CoinsDelta));
            Assert.Equal(profile.TotalXp - start.TotalXp, ledger.Sum(entry => entry.XpDelta));
            Assert.Equal(profile.Hp - start.Hp, ledger.Sum(entry => entry.HpDelta));
            Assert.Contains(ledger, entry => entry.Kind == GameLedgerEntryKind.LevelUp);
            Assert.Contains(ledger, entry => entry.Kind == GameLedgerEntryKind.Knockout);
        }

        [Fact]
        public async Task ApplyAsync_ShouldStampEntriesWithTheCurrentTime()
        {
            var timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();

            await ApplyAsync(GameLedgerEntryKind.HabitDone, new GameDelta(5, 10, 1));

            Assert.Equal(timeProvider.GetUtcNow(), Assert.Single(GameLedgerEntrySingleton.Instance).CreatedAt);
        }
    }
}
