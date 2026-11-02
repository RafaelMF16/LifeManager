using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Enums;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.Domain.Test.Ranking
{
    public class RankingReactionTests
    {
        private static readonly DateOnly Monday = new(2026, 11, 9);
        private static readonly RankingGroupId GroupId = new(1);

        [Fact]
        public void Create_ShouldRefuseAReactionToMyself()
        {
            var result = RankingReaction.Create(GroupId, Monday, new RankingGroupMemberId(1), new RankingGroupMemberId(1), RankingReactionKind.Clap, DateTimeOffset.UtcNow);

            Assert.Equal(RankingErrors.CannotReactToSelf, result.Error);
        }

        [Fact]
        public void Create_ShouldRefuseAnUnknownKind()
        {
            var result = RankingReaction.Create(GroupId, Monday, new RankingGroupMemberId(1), new RankingGroupMemberId(2), (RankingReactionKind)3, DateTimeOffset.UtcNow);

            Assert.Equal(RankingErrors.UnknownReaction, result.Error);
        }

        [Fact]
        public void ChangeKind_ShouldKeepThePair()
        {
            var reaction = RankingReaction.Create(GroupId, Monday, new RankingGroupMemberId(1), new RankingGroupMemberId(2), RankingReactionKind.Clap, DateTimeOffset.UtcNow).Value!;

            reaction.ChangeKind(RankingReactionKind.Fire, DateTimeOffset.UtcNow);

            Assert.Equal((RankingReactionKind.Fire, 1, 2), (reaction.Kind, reaction.FromMemberId.Value, reaction.ToMemberId.Value));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(-7, true)]
        [InlineData(-14, false)]
        [InlineData(7, false)]
        public void IsInWindow_ShouldOnlyOpenThisWeekAndTheLastOne(int weekOffset, bool expected)
        {
            Assert.Equal(expected, RankingReaction.IsInWindow(Monday.AddDays(weekOffset), Monday.AddDays(6)));
        }
    }
}
