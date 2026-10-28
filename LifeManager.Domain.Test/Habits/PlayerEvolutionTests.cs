using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;

namespace LifeManager.Domain.Test.Habits
{
    public class PlayerEvolutionTests
    {
        private static readonly DateOnly Today = new(2026, 10, 28);

        [Theory]
        [InlineData(EvolutionRange.Month, 30)]
        [InlineData(EvolutionRange.Quarter, 90)]
        [InlineData(EvolutionRange.Year, 365)]
        public void Days_ShouldBeTheRangeLength(EvolutionRange range, int days)
        {
            Assert.Equal(days, PlayerEvolution.Days(range));
        }

        [Fact]
        public void Start_ShouldCountTodayAsTheLastDay()
        {
            Assert.Equal(new DateOnly(2026, 9, 29), PlayerEvolution.Start(EvolutionRange.Month, Today));
        }

        [Fact]
        public void Fill_ShouldGiveOnePointPerDay_RepeatingTheLastTotalsOnEmptyDays()
        {
            var start = Today.AddDays(-3);

            var points = PlayerEvolution.Fill(start, Today, new PlayerTotals(0, 0),
                [new PlayerDayDelta(start, 20, 10), new PlayerDayDelta(Today.AddDays(-1), 30, -5)]);

            Assert.Equal(
                [
                    new PlayerEvolutionPoint(start, 20, 10),
                    new PlayerEvolutionPoint(start.AddDays(1), 20, 10),
                    new PlayerEvolutionPoint(start.AddDays(2), 50, 5),
                    new PlayerEvolutionPoint(Today, 50, 5)
                ],
                points);
        }

        [Fact]
        public void Fill_ShouldStartFromWhatCameBeforeThePeriod()
        {
            var points = PlayerEvolution.Fill(Today.AddDays(-1), Today, new PlayerTotals(400, 120), [new PlayerDayDelta(Today, 10, 5)]);

            Assert.Equal(new PlayerEvolutionPoint(Today.AddDays(-1), 400, 120), points[0]);
            Assert.Equal(new PlayerEvolutionPoint(Today, 410, 125), points[1]);
        }

        [Fact]
        public void Fill_ShouldBeFlat_WhenNothingHappened()
        {
            var points = PlayerEvolution.Fill(Today.AddDays(-29), Today, new PlayerTotals(0, 0), []);

            Assert.Equal(30, points.Count);
            Assert.All(points, point => Assert.Equal((0, 0), (point.TotalXp, point.Coins)));
        }

        [Fact]
        public void Fill_ShouldEndAtTheSumOfEverything_IgnoringDeltasOutsideThePeriod()
        {
            var points = PlayerEvolution.Fill(Today.AddDays(-2), Today, new PlayerTotals(100, 50),
                [new PlayerDayDelta(Today.AddDays(-5), 999, 999), new PlayerDayDelta(Today.AddDays(-2), 5, 5), new PlayerDayDelta(Today, 15, -20)]);

            Assert.Equal(3, points.Count);
            Assert.Equal(new PlayerEvolutionPoint(Today, 120, 35), points[^1]);
        }
    }
}
