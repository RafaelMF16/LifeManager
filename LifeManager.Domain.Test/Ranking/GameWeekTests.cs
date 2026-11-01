using LifeManager.Domain.Ranking;

namespace LifeManager.Domain.Test.Ranking
{
    public class GameWeekTests
    {
        private static readonly DateOnly Monday = new(2026, 11, 2);

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(6)]
        public void Start_ShouldBeTheMondayOfTheWeek(int offset)
        {
            Assert.Equal(Monday, GameWeek.Start(Monday.AddDays(offset)));
        }

        [Fact]
        public void End_ShouldBeTheSunday()
        {
            Assert.Equal(new DateOnly(2026, 11, 8), GameWeek.End(Monday));
        }

        [Fact]
        public void IsWeekStart_ShouldOnlyAcceptMondays()
        {
            Assert.True(GameWeek.IsWeekStart(Monday));
            Assert.False(GameWeek.IsWeekStart(Monday.AddDays(1)));
        }

        [Theory]
        [InlineData(6, false)] // Sunday itself
        [InlineData(7, false)] // Monday: Sunday can still be checked in
        [InlineData(8, true)]  // Tuesday: Sunday is no longer editable
        public void IsSettled_ShouldWaitUntilSundayCanNoLongerBeEdited(int todayOffset, bool expected)
        {
            Assert.Equal(expected, GameWeek.IsSettled(Monday, Monday.AddDays(todayOffset)));
        }
    
        [Theory]
        [InlineData(0, -14)] // Monday: last week's Sunday can still be checked in
        [InlineData(1, -7)]  // Tuesday: last week is settled
        [InlineData(6, -7)]  // Sunday
        public void LastSettledStart_ShouldBeTheLatestSettledWeek(int todayOffset, int expectedOffset)
        {
            var today = Monday.AddDays(todayOffset);

            Assert.Equal(Monday.AddDays(expectedOffset), GameWeek.LastSettledStart(today));
            Assert.True(GameWeek.IsSettled(GameWeek.LastSettledStart(today), today));
            Assert.False(GameWeek.IsSettled(GameWeek.LastSettledStart(today).AddDays(7), today));
        }
    }
}
