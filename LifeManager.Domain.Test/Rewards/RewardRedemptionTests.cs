using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Errors;

namespace LifeManager.Domain.Test.Rewards
{
    public class RewardRedemptionTests
    {
        private static readonly DateOnly Today = new(2026, 10, 8);
        private static readonly DateTimeOffset Now = new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);

        private static Reward StoredReward(DateTimeOffset? archivedAt = null)
            => Reward.FromPersistence(7, 1, "Video games", 50, "gamepad-2", Now.AddDays(-10), archivedAt);

        [Fact]
        public void Create_ShouldCopyTheRewardAsItIsNow()
        {
            var result = RewardRedemption.Create(StoredReward(), Today, Now);

            Assert.True(result.IsSuccess);
            var redemption = result.Value;
            Assert.Equal(7, redemption.RewardId.Value);
            Assert.Equal(1, redemption.UserId.Value);
            Assert.Equal("Video games", redemption.RewardName);
            Assert.Equal("gamepad-2", redemption.RewardIcon);
            Assert.Equal(50, redemption.CostPaid);
            Assert.Equal(Today, redemption.RedeemedOn);
            Assert.Equal(Now, redemption.RedeemedAt);
            Assert.False(redemption.IsUndone);
        }

        [Fact]
        public void Create_ShouldFail_WhenRewardIsArchived()
        {
            var result = RewardRedemption.Create(StoredReward(archivedAt: Now), Today, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.Archived, result.Error);
        }

        [Fact]
        public void Undo_ShouldMarkItUndone_OnTheSameDay()
        {
            var redemption = RewardRedemption.Create(StoredReward(), Today, Now).Value;
            Assert.True(redemption.CanUndo(Today));

            var result = redemption.Undo(Today, Now.AddHours(1));

            Assert.True(result.IsSuccess);
            Assert.Equal(Now.AddHours(1), redemption.UndoneAt);
            Assert.False(redemption.CanUndo(Today));
        }

        [Fact]
        public void Undo_ShouldFail_OnALaterDay()
        {
            var redemption = RewardRedemption.Create(StoredReward(), Today, Now).Value;

            var result = redemption.Undo(Today.AddDays(1), Now.AddDays(1));

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.UndoOutsideWindow, result.Error);
            Assert.False(redemption.CanUndo(Today.AddDays(1)));
            Assert.Null(redemption.UndoneAt);
        }

        [Fact]
        public void Undo_ShouldFail_WhenAlreadyUndone()
        {
            var redemption = RewardRedemption.Create(StoredReward(), Today, Now).Value;
            redemption.Undo(Today, Now);

            var result = redemption.Undo(Today, Now);

            Assert.False(result.IsSuccess);
            Assert.Equal(RewardErrors.AlreadyUndone, result.Error);
        }
    }
}
