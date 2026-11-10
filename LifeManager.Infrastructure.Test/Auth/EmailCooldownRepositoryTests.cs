using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Auth;
using LifeManager.Infrastructure.Test.Seed;

namespace LifeManager.Infrastructure.Test.Auth
{
    public class EmailCooldownRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly Email Address = Email.Create("ana@lifemanager.test").Unwrap();

        [Fact]
        public async Task TryReserveAsync_ShouldRefuse_WhenTheSameAddressInAnotherCaseIsInsideTheCooldown()
        {
            await using var context = Fixture.CreateContext();
            var repository = new EmailCooldownRepository(context);

            var first = await repository.TryReserveAsync(Address, EmailKind.Confirmation, TestData.Now, default);
            var second = await repository.TryReserveAsync(
                Email.Create("ANA@LifeManager.test").Unwrap(), EmailKind.Confirmation, TestData.Now.AddSeconds(59), default);

            Assert.True(first);
            Assert.False(second);
        }

        [Fact]
        public async Task TryReserveAsync_ShouldReserveAgain_WhenTheCooldownHasPassed()
        {
            await using var context = Fixture.CreateContext();
            var repository = new EmailCooldownRepository(context);
            await repository.TryReserveAsync(Address, EmailKind.PasswordReset, TestData.Now, default);

            var afterCooldown = await repository.TryReserveAsync(Address, EmailKind.PasswordReset, TestData.Now + EmailCooldown.Duration, default);
            var rightAfter = await repository.TryReserveAsync(Address, EmailKind.PasswordReset, TestData.Now + EmailCooldown.Duration, default);

            Assert.True(afterCooldown);
            Assert.False(rightAfter);
        }

        [Fact]
        public async Task TryReserveAsync_ShouldKeepEachKindApart_WhenAnotherKindWasJustReserved()
        {
            await using var context = Fixture.CreateContext();
            var repository = new EmailCooldownRepository(context);
            await repository.TryReserveAsync(Address, EmailKind.Confirmation, TestData.Now, default);

            Assert.True(await repository.TryReserveAsync(Address, EmailKind.AccountExists, TestData.Now, default));
        }

        [Fact]
        public async Task TryReserveAsync_ShouldReserveOnce_WhenFiveRequestsRunConcurrently()
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
            {
                await using var context = Fixture.CreateContext();
                return await new EmailCooldownRepository(context).TryReserveAsync(Address, EmailKind.Confirmation, TestData.Now, default);
            }));

            Assert.Equal(1, results.Count(reserved => reserved));
        }
    }
}
