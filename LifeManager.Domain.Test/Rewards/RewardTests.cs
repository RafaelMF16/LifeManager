using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Rewards.ValueObjects;

namespace LifeManager.Domain.Test.Rewards
{
    public class RewardTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);

        private static Reward CreateReward(string name = "Video games", int cost = 50, string? icon = "gamepad-2")
            => Reward.Create(1, name, cost, icon, Now).Value;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RewardName_ShouldReturnFailure_WhenValueIsNullOrWhiteSpace(string? value)
        {
            var result = RewardName.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.NameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void RewardName_ShouldReturnFailure_WhenValueIsLongerThanMaxLength()
        {
            var result = RewardName.Create(new string('a', RewardName.MaxLength + 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.NameTooLong, result.Error);
        }

        [Fact]
        public void RewardName_ShouldTrimAndNormalize()
        {
            var result = RewardName.Create("  Pão de Açúcar ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Pão de Açúcar", result.Value.Value);
            Assert.Equal("pao de acucar", result.Value.NormalizedValue);
        }

        [Fact]
        public void Create_ShouldReturnActiveReward_WithTrimmedIcon()
        {
            var result = Reward.Create(1, " Pizza night ", 120, "  pizza ", Now);

            Assert.True(result.IsSuccess);
            var reward = result.Value;
            Assert.Equal("Pizza night", reward.Name.Value);
            Assert.Equal("pizza night", reward.NormalizedName);
            Assert.Equal(120, reward.Cost);
            Assert.Equal("pizza", reward.Icon);
            Assert.Equal(Now, reward.CreatedAt);
            Assert.False(reward.IsArchived);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Create_ShouldStoreNullIcon_WhenBlank(string? icon)
        {
            var reward = Reward.Create(1, "Nap", 10, icon, Now).Value;

            Assert.Null(reward.Icon);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(Reward.MaxCost + 1)]
        public void Create_ShouldReturnFailure_WhenCostIsOutOfRange(int cost)
        {
            var result = Reward.Create(1, "Nap", cost, null, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.InvalidCost, result.Error);
        }

        [Theory]
        [InlineData(Reward.MinCost)]
        [InlineData(Reward.MaxCost)]
        public void Create_ShouldAcceptCostLimits(int cost)
        {
            Assert.True(Reward.Create(1, "Nap", cost, null, Now).IsSuccess);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenIconIsTooLong()
        {
            var result = Reward.Create(1, "Nap", 10, new string('a', Reward.IconMaxLength + 1), Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.IconTooLong, result.Error);
        }

        [Fact]
        public void Update_ShouldChangeNameCostAndIcon()
        {
            var reward = CreateReward();

            var result = reward.Update("Movie night", 80, null);

            Assert.True(result.IsSuccess);
            Assert.Equal("Movie night", reward.Name.Value);
            Assert.Equal("movie night", reward.NormalizedName);
            Assert.Equal(80, reward.Cost);
            Assert.Null(reward.Icon);
        }

        [Fact]
        public void Update_ShouldKeepValues_WhenInvalid()
        {
            var reward = CreateReward();

            var result = reward.Update("Movie night", 0, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.InvalidCost, result.Error);
            Assert.Equal("Video games", reward.Name.Value);
            Assert.Equal(50, reward.Cost);
        }

        [Fact]
        public void Update_ShouldReturnFailure_WhenArchived()
        {
            var reward = CreateReward();
            reward.Archive(Now);

            var result = reward.Update("Movie night", 80, null);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.Archived, result.Error);
        }

        [Fact]
        public void ArchiveAndRestore_ShouldToggleArchivedAt()
        {
            var reward = CreateReward();

            Assert.True(reward.Archive(Now).IsSuccess);
            Assert.Equal(Now, reward.ArchivedAt);
            Assert.Equal(RewardErrors.AlreadyArchived, reward.Archive(Now).Error);

            Assert.True(reward.Restore().IsSuccess);
            Assert.Null(reward.ArchivedAt);
            Assert.Equal(RewardErrors.NotArchived, reward.Restore().Error);
        }

        [Fact]
        public void EnsureCanRedeem_ShouldFail_WhenArchived()
        {
            var reward = CreateReward();
            Assert.True(reward.EnsureCanRedeem().IsSuccess);

            reward.Archive(Now);

            Assert.Equal(RewardErrors.Archived, reward.EnsureCanRedeem().Error);
        }
    }
}
