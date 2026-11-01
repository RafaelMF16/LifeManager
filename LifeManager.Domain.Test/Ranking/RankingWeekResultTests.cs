using LifeManager.Domain.Ranking;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Ranking
{
    public class RankingWeekResultTests
    {
        private static readonly DateOnly Monday = new(2026, 11, 2);

        private static RankingWeekResult Line(int position, int points = 100, bool eligible = true, int eligibleCount = 3)
            => RankingWeekResult.FromPersistence(1, 1, Monday, 1, points, 0, 0, position, eligible, eligibleCount, eligibleCount, DateTimeOffset.UtcNow);

        [Fact]
        public void ForWeek_ShouldMarkWhoWasInFromMonday_AndCountThem()
        {
            var standings = new[] { new Standing(new UserId(1), 50, 1, 5, 1), new Standing(new UserId(2), 20, 0, 2, 2) };
            var joinedOn = new Dictionary<UserId, DateOnly> { [new UserId(1)] = Monday, [new UserId(2)] = Monday.AddDays(1) };

            var results = RankingWeekResult.ForWeek(new RankingGroupId(1), Monday, standings, joinedOn, DateTimeOffset.UtcNow);

            Assert.Equal([true, false], results.Select(result => result.Eligible));
            Assert.All(results, result => Assert.Equal((2, 1), (result.MemberCount, result.EligibleCount)));
        }

        [Fact]
        public void IsWinAndIsPodium_ShouldNeedThreeEligibleMembersPointsAndEligibility()
        {
            Assert.True(Line(1).IsWin);
            Assert.True(Line(3).IsPodium);
            Assert.False(Line(2).IsWin);
            Assert.False(Line(4).IsPodium);
            Assert.False(Line(1, eligibleCount: 2).IsWin);
            Assert.False(Line(1, points: 0).IsWin);
            Assert.False(Line(1, eligible: false).IsPodium);
        }

        [Theory]
        [InlineData(5, false)] // Friday: Tuesday + 3 days
        [InlineData(6, true)]
        public void IsPastGrace_ShouldWaitThreeDaysPastTuesday(int daysAfterSunday, bool expected)
        {
            Assert.Equal(expected, RankingWeekResult.IsPastGrace(Monday, GameWeek.End(Monday).AddDays(daysAfterSunday)));
        }
    }
}
