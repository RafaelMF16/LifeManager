using LifeManager.Application.Shared.Time;
using LifeManager.Application.Test.Configurations;
using Microsoft.Extensions.Configuration;

namespace LifeManager.Application.Test.Shared
{
    public class AppClockTest
    {
        private static AppClock ClockAt(DateTimeOffset utcNow)
            => new(new FakeTimeProvider { UtcNow = utcNow }, new ConfigurationBuilder().Build());

        [Fact]
        public void CurrentHour_ShouldBeTheHourInTheBusinessTimeZone()
        {
            // 23:30 UTC is 20:30 in São Paulo (UTC−3).
            var clock = ClockAt(new DateTimeOffset(2026, 11, 11, 23, 30, 0, TimeSpan.Zero));

            Assert.Equal(new BusinessHour(new DateOnly(2026, 11, 11), 20), clock.CurrentHour());
        }

        [Fact]
        public void CurrentHour_ShouldFollowTheBusinessDay_WhenUtcIsAlreadyTomorrow()
        {
            // 02:10 UTC on the 12th is 23:10 on the 11th in São Paulo.
            var clock = ClockAt(new DateTimeOffset(2026, 11, 12, 2, 10, 0, TimeSpan.Zero));

            Assert.Equal(new BusinessHour(new DateOnly(2026, 11, 11), 23), clock.CurrentHour());
        }
    }
}
