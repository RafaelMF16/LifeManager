using LifeManager.Domain.Auth;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Auth;
using LifeManager.Infrastructure.Test.Seed;

namespace LifeManager.Infrastructure.Test.Auth
{
    public class LoginAttemptRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private const string Ip = "203.0.113.7";
        private static readonly Email Address = Email.Create("ana@lifemanager.test").Unwrap();

        [Fact]
        public async Task RecordFailureAsync_ShouldCountUp_WhenFailuresFallInsideTheWindow()
        {
            await using var context = Fixture.CreateContext();
            var repository = new LoginAttemptRepository(context);

            for (var minute = 0; minute < LoginAttempt.MaxFailures; minute++)
                await repository.RecordFailureAsync(Address, Ip, TestData.Now.AddMinutes(minute), default);

            var attempt = await repository.GetAsync(Address, Ip, default);
            Assert.NotNull(attempt);
            Assert.Equal(LoginAttempt.MaxFailures, attempt.FailedCount);
            Assert.Equal(TestData.Now, attempt.WindowStartedAt);
            Assert.True(attempt.IsBlocked(TestData.Now.AddMinutes(LoginAttempt.MaxFailures)));
        }

        [Fact]
        public async Task RecordFailureAsync_ShouldStartOver_WhenTheWindowHasEnded()
        {
            await using var context = Fixture.CreateContext();
            var repository = new LoginAttemptRepository(context);
            await repository.RecordFailureAsync(Address, Ip, TestData.Now, default);
            await repository.RecordFailureAsync(Address, Ip, TestData.Now.AddMinutes(1), default);

            var restartedAt = TestData.Now + LoginAttempt.Window;
            await repository.RecordFailureAsync(Address, Ip, restartedAt, default);

            var attempt = await repository.GetAsync(Address, Ip, default);
            Assert.NotNull(attempt);
            Assert.Equal(1, attempt.FailedCount);
            Assert.Equal(restartedAt, attempt.WindowStartedAt);
        }

        [Fact]
        public async Task RecordFailureAsync_ShouldLoseNoFailure_WhenFailuresRunConcurrently()
        {
            await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
            {
                await using var context = Fixture.CreateContext();
                await new LoginAttemptRepository(context).RecordFailureAsync(Address, Ip, TestData.Now, default);
            }));

            await using var readContext = Fixture.CreateContext();
            var attempt = await new LoginAttemptRepository(readContext).GetAsync(Address, Ip, default);
            Assert.NotNull(attempt);
            Assert.Equal(5, attempt.FailedCount);
        }

        [Fact]
        public async Task ClearAsync_ShouldDeleteOnlyThatPair_WhenTheLoginSucceeds()
        {
            await using var context = Fixture.CreateContext();
            var repository = new LoginAttemptRepository(context);
            await repository.RecordFailureAsync(Address, Ip, TestData.Now, default);
            await repository.RecordFailureAsync(Address, "198.51.100.2", TestData.Now, default);

            await repository.ClearAsync(Email.Create("ANA@lifemanager.test").Unwrap(), Ip, default);

            Assert.Null(await repository.GetAsync(Address, Ip, default));
            Assert.NotNull(await repository.GetAsync(Address, "198.51.100.2", default));
        }
    }
}
