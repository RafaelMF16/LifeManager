using LifeManager.Domain.Ranking;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Test.Ranking
{
    public class GroupStandingsTests
    {
        private static readonly DateOnly Monday = new(2026, 11, 2);
        private static readonly UserId Ana = new(1);
        private static readonly UserId Bia = new(2);
        private static readonly UserId Caio = new(3);
        private static readonly UserId Duda = new(4);

        private static (UserId, WeeklyScoreResult) Score(UserId userId, int points, int perfectDays = 0, int keptDays = 0)
            => (userId, new WeeklyScoreResult(Monday, points, keptDays, perfectDays, []));

        [Fact]
        public void Rank_ShouldOrderByPointsThenPerfectDaysThenKeptDays()
        {
            var standings = GroupStandings.Rank([Score(Ana, 100), Score(Bia, 200), Score(Caio, 100, perfectDays: 2), Score(Duda, 100, perfectDays: 2, keptDays: 9)]);

            Assert.Equal([Bia, Duda, Caio, Ana], standings.Select(standing => standing.UserId));
            Assert.Equal([1, 2, 3, 4], standings.Select(standing => standing.Position));
        }

        [Fact]
        public void Rank_ShouldShareThePosition_OnAFullTie()
        {
            var standings = GroupStandings.Rank([Score(Ana, 100, 1, 5), Score(Bia, 100, 1, 5), Score(Caio, 50)]);

            Assert.Equal([1, 1, 3], standings.Select(standing => standing.Position));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(-3, true)]
        [InlineData(1, false)]
        public void IsEligible_ShouldNeedToBeInFromMonday(int joinedOffset, bool expected)
        {
            Assert.Equal(expected, GroupStandings.IsEligible(Monday.AddDays(joinedOffset), Monday));
        }

        [Fact]
        public void CountsForAchievements_ShouldNeedThreeEligibleMembers()
        {
            Assert.False(GroupStandings.CountsForAchievements(2));
            Assert.True(GroupStandings.CountsForAchievements(3));
        }

        [Fact]
        public void OvertakenBy_ShouldListWhoWasBehindOrTiedAndIsNowAhead()
        {
            var before = GroupStandings.Rank([Score(Ana, 50), Score(Bia, 30), Score(Caio, 50), Score(Duda, 90)]);
            var now = GroupStandings.Rank([Score(Ana, 50), Score(Bia, 60), Score(Caio, 70), Score(Duda, 100)]);

            var overtakers = GroupStandings.OvertakenBy(before, now, Ana);

            Assert.Equal([Caio, Bia], overtakers);
        }

        [Fact]
        public void OvertakenBy_ShouldBeEmpty_WhenNobodyPassed()
        {
            var before = GroupStandings.Rank([Score(Ana, 50), Score(Bia, 30)]);
            var now = GroupStandings.Rank([Score(Ana, 60), Score(Bia, 40)]);

            Assert.Empty(GroupStandings.OvertakenBy(before, now, Ana));
        }

        [Fact]
        public void Next_ShouldBeTheClosestAhead_OrNullWhenFirst()
        {
            var standings = GroupStandings.Rank([Score(Ana, 50), Score(Bia, 80), Score(Caio, 200)]);

            Assert.Equal(Bia, GroupStandings.Next(standings, Ana)!.UserId);
            Assert.Null(GroupStandings.Next(standings, Caio));
        }

        [Fact]
        public void Next_ShouldBeNull_WhenTiedForFirst()
        {
            var standings = GroupStandings.Rank([Score(Ana, 80), Score(Bia, 80)]);

            Assert.Null(GroupStandings.Next(standings, Bia));
        }
    }
}
