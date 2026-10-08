using LifeManager.Application.Rewards.DTOs;
using LifeManager.Application.Rewards.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Enums;
using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Rewards
{
    [Collection("ApplicationServices")]
    public class RewardServiceTest : BaseTest
    {
        private static readonly UserId UserId = new(1);
        private static readonly UserId OtherUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 8);

        private readonly RewardService _service;

        public RewardServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<RewardService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            RewardSingleton.Instance.Clear();
        }

        private static Reward SeedReward(string name, int cost = 50, UserId? userId = null, bool archived = false)
        {
            var reward = Reward.FromPersistence(
                RewardSingleton.Instance.Count + 1,
                (userId ?? UserId).Value,
                name,
                cost,
                null,
                new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
                archived ? new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero) : null);
            RewardSingleton.Instance.Add(reward);

            return reward;
        }

        [Fact]
        public async Task CreateAsync_ShouldStoreTheReward()
        {
            var result = await _service.CreateAsync(new RewardDto("  Video games ", 60, "gamepad-2"), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Video games", result.Value.Name);
            Assert.Equal(60, result.Value.Cost);
            Assert.Equal("gamepad-2", result.Value.Icon);
            Assert.Null(result.Value.ArchivedAt);
            Assert.Single(RewardSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldFail_WhenAnActiveRewardHasTheName()
        {
            SeedReward("Vídeo games");

            var result = await _service.CreateAsync(new RewardDto("video GAMES", 60, null), UserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.NameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowTheNameOfAnArchivedOrAnotherUsersReward()
        {
            SeedReward("Video games", archived: true);
            SeedReward("Video games", userId: OtherUserId);

            var result = await _service.CreateAsync(new RewardDto("Video games", 60, null), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnValidationError_WhenCostIsInvalid()
        {
            var result = await _service.CreateAsync(new RewardDto("Video games", 0, null), UserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.InvalidCost, result.Error);
            Assert.Empty(RewardSingleton.Instance);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFilterByStatusAndSortByCost()
        {
            SeedReward("Pizza", cost: 120);
            SeedReward("Nap", cost: 10);
            SeedReward("Movie", cost: 80);
            SeedReward("Old", cost: 5, archived: true);
            SeedReward("Other", cost: 1, userId: OtherUserId);

            var active = await _service.GetPagedAsync(new RewardListQueryDto { SortBy = RewardSortBy.Cost }, UserId, CancellationToken.None);
            var archived = await _service.GetPagedAsync(new RewardListQueryDto { Status = RewardStatusFilter.Archived }, UserId, CancellationToken.None);
            var byNameDesc = await _service.GetPagedAsync(
                new RewardListQueryDto { SortBy = RewardSortBy.Name, SortDirection = SortDirection.Desc }, UserId, CancellationToken.None);

            Assert.Equal(["Nap", "Movie", "Pizza"], active.Value.Items.Select(reward => reward.Name));
            Assert.Equal(["Old"], archived.Value.Items.Select(reward => reward.Name));
            Assert.Equal(["Pizza", "Nap", "Movie"], byNameDesc.Value.Items.Select(reward => reward.Name));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSearchIgnoringAccentsAndCase()
        {
            SeedReward("Café especial");
            SeedReward("Pizza");

            var result = await _service.GetPagedAsync(new RewardListQueryDto { Search = "CAFE" }, UserId, CancellationToken.None);

            Assert.Equal(["Café especial"], result.Value.Items.Select(reward => reward.Name));
        }

        [Fact]
        public async Task UpdateAsync_ShouldSaveTheChanges()
        {
            var reward = SeedReward("Pizza", cost: 120);

            var result = await _service.UpdateAsync(reward.Id!.Value, new RewardDto("Pizza night", 150, "pizza"), UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var stored = RewardSingleton.Instance.Single();
            Assert.Equal("Pizza night", stored.Name.Value);
            Assert.Equal(150, stored.Cost);
            Assert.Equal("pizza", stored.Icon);
        }

        [Fact]
        public async Task UpdateAsync_ShouldFail_WhenAnotherActiveRewardHasTheName()
        {
            SeedReward("Nap");
            var reward = SeedReward("Pizza");

            var result = await _service.UpdateAsync(reward.Id!.Value, new RewardDto("nap", 10, null), UserId, CancellationToken.None);

            Assert.Equal(RewardErrors.NameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_ForAnotherUsersReward()
        {
            var reward = SeedReward("Pizza", userId: OtherUserId);

            var result = await _service.UpdateAsync(reward.Id!.Value, new RewardDto("Pizza", 10, null), UserId, CancellationToken.None);

            Assert.Equal(RewardErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task ArchiveAsync_ShouldArchive_AndFailTheSecondTime()
        {
            var reward = SeedReward("Pizza");

            var first = await _service.ArchiveAsync(reward.Id!.Value, UserId, CancellationToken.None);
            var second = await _service.ArchiveAsync(reward.Id!.Value, UserId, CancellationToken.None);

            Assert.True(first.IsSuccess);
            Assert.True(RewardSingleton.Instance.Single().IsArchived);
            Assert.Equal(RewardErrors.AlreadyArchived, second.Error);
        }

        [Fact]
        public async Task RestoreAsync_ShouldBringItBack()
        {
            var reward = SeedReward("Pizza", archived: true);

            var result = await _service.RestoreAsync(reward.Id!.Value, UserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.ArchivedAt);
            Assert.False(RewardSingleton.Instance.Single().IsArchived);
        }

        [Fact]
        public async Task RestoreAsync_ShouldFail_WhenAnActiveRewardTookTheName()
        {
            var archived = SeedReward("Pizza", archived: true);
            SeedReward("Pizza");

            var result = await _service.RestoreAsync(archived.Id!.Value, UserId, CancellationToken.None);

            Assert.Equal(RewardErrors.NameAlreadyExists, result.Error);
            Assert.True(RewardSingleton.Instance.Single(reward => reward.Id == archived.Id).IsArchived);
        }
    }
}
