using LifeManager.Domain.Auth;

namespace LifeManager.Domain.Test.Auth
{
    public class LoginAttemptTests
    {
        private static readonly DateTimeOffset FirstFailure = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        private static LoginAttempt FailedTimes(int count)
        {
            var attempt = LoginAttempt.Create("ana@example.com", "203.0.113.7", FirstFailure);
            for (var failure = 1; failure < count; failure++)
                attempt.RecordFailure(FirstFailure.AddMinutes(failure));

            return attempt;
        }

        [Fact]
        public void Create_ShouldCountTheFirstFailure_AndKeyByTheLowercaseEmail()
        {
            var attempt = LoginAttempt.Create("Ana@Example.com", "203.0.113.7", FirstFailure);

            Assert.Equal("ana@example.com", attempt.Email);
            Assert.Equal("203.0.113.7", attempt.IpAddress);
            Assert.Equal(1, attempt.FailedCount);
            Assert.Equal(FirstFailure, attempt.WindowStartedAt);
        }

        [Fact]
        public void IsBlocked_ShouldBeFalse_WhenFewerFailuresThanTheLimit()
        {
            var attempt = FailedTimes(LoginAttempt.MaxFailures - 1);

            Assert.False(attempt.IsBlocked(FirstFailure.AddMinutes(10)));
        }

        [Fact]
        public void IsBlocked_ShouldBeTrue_WhenTheLimitIsReachedInsideTheWindow()
        {
            var attempt = FailedTimes(LoginAttempt.MaxFailures);

            Assert.True(attempt.IsBlocked(FirstFailure.AddMinutes(10)));
        }

        [Fact]
        public void IsBlocked_ShouldBeFalse_WhenTheWindowIsOver()
        {
            var attempt = FailedTimes(LoginAttempt.MaxFailures);

            Assert.False(attempt.IsBlocked(FirstFailure.Add(LoginAttempt.Window)));
        }

        [Fact]
        public void RecordFailure_ShouldCountInsideTheWindow()
        {
            var attempt = FailedTimes(2);

            attempt.RecordFailure(FirstFailure.AddMinutes(14));

            Assert.Equal(3, attempt.FailedCount);
            Assert.Equal(FirstFailure, attempt.WindowStartedAt);
        }

        [Fact]
        public void RecordFailure_ShouldStartANewWindow_WhenTheWindowIsOver()
        {
            var attempt = FailedTimes(LoginAttempt.MaxFailures);
            var later = FirstFailure.Add(LoginAttempt.Window).AddMinutes(1);

            attempt.RecordFailure(later);

            Assert.Equal(1, attempt.FailedCount);
            Assert.Equal(later, attempt.WindowStartedAt);
        }
    }
}
