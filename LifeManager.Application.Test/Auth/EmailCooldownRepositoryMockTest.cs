using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Auth
{
    // The services rely on the mock behaving like the real conditional upsert, so its rule is tested on its own.
    [Collection("ApplicationServices")]
    public class EmailCooldownRepositoryMockTest : BaseTest
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        private static readonly Email AnaEmail = Email.Create("ana@example.com").Value!;

        private readonly IEmailCooldownRepository _repository;

        public EmailCooldownRepositoryMockTest()
        {
            EmailCooldownSingleton.Instance.Clear();
            _repository = ServiceProvider.GetRequiredService<IEmailCooldownRepository>();
            Assert.IsType<EmailCooldownRepositoryMock>(_repository);
        }

        [Fact]
        public async Task TryReserveAsync_ShouldReserve_WhenNothingWasSentYet()
        {
            Assert.True(await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now, CancellationToken.None));
        }

        [Fact]
        public async Task TryReserveAsync_ShouldNotReserve_WhenInsideTheCooldown()
        {
            await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now, CancellationToken.None);

            Assert.False(await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now.AddSeconds(59), CancellationToken.None));
        }

        [Fact]
        public async Task TryReserveAsync_ShouldReserveAgain_WhenTheCooldownPassed()
        {
            await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now, CancellationToken.None);

            Assert.True(await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now.Add(EmailCooldown.Duration), CancellationToken.None));
        }

        [Fact]
        public async Task TryReserveAsync_ShouldKeepEachKindApart()
        {
            await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now, CancellationToken.None);

            Assert.True(await _repository.TryReserveAsync(AnaEmail, EmailKind.PasswordReset, Now, CancellationToken.None));
        }

        [Fact]
        public async Task TryReserveAsync_ShouldIgnoreTheEmailCase()
        {
            await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now, CancellationToken.None);

            var upper = Email.Create("ANA@example.com").Value!;
            Assert.False(await _repository.TryReserveAsync(upper, EmailKind.Confirmation, Now, CancellationToken.None));
        }

        [Fact]
        public async Task DeleteOlderThanAsync_ShouldDeleteOnlyTheOldOnes()
        {
            await _repository.TryReserveAsync(AnaEmail, EmailKind.Confirmation, Now.AddDays(-2), CancellationToken.None);
            await _repository.TryReserveAsync(AnaEmail, EmailKind.PasswordReset, Now, CancellationToken.None);

            var deleted = await _repository.DeleteOlderThanAsync(Now.AddDays(-1), CancellationToken.None);

            Assert.Equal(1, deleted);
            Assert.Equal(EmailKind.PasswordReset, Assert.Single(EmailCooldownSingleton.Instance).Kind);
        }
    }
}
