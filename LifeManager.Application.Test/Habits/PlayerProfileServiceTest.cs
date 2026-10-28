using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Habits
{
    [Collection("ApplicationServices")]
    public class PlayerProfileServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly DateOnly Today = new(2026, 10, 28);

        private readonly PlayerProfileService _playerProfileService;
        private readonly PlayerWalletService _playerWalletService;

        public PlayerProfileServiceTest()
        {
            _playerProfileService = ServiceProvider.GetRequiredService<PlayerProfileService>();
            _playerWalletService = ServiceProvider.GetRequiredService<PlayerWalletService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            PlayerProfileSingleton.Instance.Clear();
            GameLedgerEntrySingleton.Instance.Clear();
            AchievementUnlockSingleton.Instance.Clear();
            HabitSingleton.Instance.Clear();
            HabitCheckInSingleton.Instance.Clear();
            RewardRedemptionSingleton.Instance.Clear();
        }

        private static void SeedProfile()
            => PlayerProfileSingleton.Instance.Add(PlayerProfile.FromPersistence(1, UserId.Value, 0, GameRules.MaxHp, GameRules.MaxHp, 0, 0));

        private static void SeedUnlock(AchievementCode code)
            => AchievementUnlockSingleton.Instance.Add(AchievementUnlock.FromPersistence(
                AchievementUnlockSingleton.Instance.Count + 1, UserId.Value, code, Today, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        private Task<Domain.Shared.Results.Result<PlayerProfileResponseDto>> UpdateAsync(PlayerAvatar avatar, AchievementCode? title)
            => _playerProfileService.UpdateAppearanceAsync(new UpdateAppearanceRequestDto(avatar, title), UserId, CancellationToken.None);

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldSaveAndReturnTheAppearance_WhenTheTitleIsUnlocked()
        {
            SeedProfile();
            SeedUnlock(AchievementCode.CheckIns10);

            var result = await UpdateAsync(PlayerAvatar.Rocket, AchievementCode.CheckIns10);

            Assert.True(result.IsSuccess);
            Assert.Equal(PlayerAvatar.Rocket, result.Value!.Avatar);
            Assert.Equal(AchievementCode.CheckIns10, result.Value.Title);
            Assert.Empty(result.Value.NewAchievements);

            var profile = await _playerWalletService.GetProfileAsync(UserId, CancellationToken.None);
            Assert.Equal(PlayerAvatar.Rocket, profile.Avatar);
            Assert.Equal(AchievementCode.CheckIns10, profile.Title);
        }

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldSaveNothing_WhenTheTitleIsLocked()
        {
            SeedProfile();
            SeedUnlock(AchievementCode.CheckIns10);

            var result = await UpdateAsync(PlayerAvatar.Rocket, AchievementCode.Level25);

            Assert.Equal(PlayerProfileErrors.TitleLocked, result.Error);
            var stored = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(PlayerAvatar.Cat, stored.Avatar);
            Assert.Null(stored.Title);
        }

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldCreateTheProfile_WhenTheUserHasNoneYet()
        {
            var result = await UpdateAsync(PlayerAvatar.Panda, null);

            Assert.True(result.IsSuccess);
            var stored = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(UserId, stored.UserId);
            Assert.Equal(PlayerAvatar.Panda, stored.Avatar);
            Assert.Equal(0, stored.Coins);
            Assert.Empty(GameLedgerEntrySingleton.Instance);
        }

        [Fact]
        public async Task UpdateAppearanceAsync_ShouldKeepTheAppearance_WhenTheWalletChangesAfterwards()
        {
            SeedProfile();
            await UpdateAsync(PlayerAvatar.Dog, null);

            await _playerWalletService.ApplyAsync(UserId, GameLedgerEntryKind.HabitDone, new GameDelta(10, 20, 0), Today, "Read", CancellationToken.None);

            var stored = PlayerProfileSingleton.Instance.Single();
            Assert.Equal(PlayerAvatar.Dog, stored.Avatar);
            Assert.Equal(10, stored.Coins);
        }

        [Fact]
        public async Task GetProfileAsync_ShouldReturnTheDefaultAppearance_WhenThePlayerNeverChangedIt()
        {
            var profile = await _playerWalletService.GetProfileAsync(UserId, CancellationToken.None);

            Assert.Equal(PlayerAvatar.Cat, profile.Avatar);
            Assert.Null(profile.Title);
        }
    }
}
