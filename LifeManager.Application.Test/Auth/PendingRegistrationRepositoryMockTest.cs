using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.UsersPreferences.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Auth
{
    // The services rely on the mock behaving like the real upsert and conditional completion, so it is tested on its own.
    [Collection("ApplicationServices")]
    public class PendingRegistrationRepositoryMockTest : BaseTest
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private readonly IPendingRegistrationRepository _repository;

        public PendingRegistrationRepositoryMockTest()
        {
            PendingRegistrationSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            UserPreferencesSingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            _repository = ServiceProvider.GetRequiredService<IPendingRegistrationRepository>();
            Assert.IsType<PendingRegistrationRepositoryMock>(_repository);
        }

        private static PendingRegistration NewPending(string token, string name = "Ana", Language language = Language.EN)
            => PendingRegistration.Create(name, "ana@example.com", language, Theme.Dark, EmailTokenHash.FromToken(token).Value!, Now).Value!;

        private static User NewUser() => User.Create("Ana", "ana@example.com", "hash").Value!;

        [Fact]
        public async Task SaveAsync_ShouldReplaceTheStoredOne_WhenTheEmailIsAlreadyPending()
        {
            await _repository.SaveAsync(NewPending("first"), CancellationToken.None);

            await _repository.SaveAsync(NewPending("second", name: "Ana Souza"), CancellationToken.None);

            var stored = Assert.Single(PendingRegistrationSingleton.Instance);
            Assert.Equal("Ana Souza", stored.Name.Value);
            Assert.Null(await _repository.GetByTokenHashAsync(EmailTokenHash.FromToken("first").Value!, CancellationToken.None));
            Assert.NotNull(await _repository.GetByTokenHashAsync(EmailTokenHash.FromToken("second").Value!, CancellationToken.None));
        }

        [Fact]
        public async Task CompleteAsync_ShouldCreateTheAccountAndDeleteTheSignUp()
        {
            await _repository.SaveAsync(NewPending("token"), CancellationToken.None);
            var pending = (await _repository.GetByTokenHashAsync(EmailTokenHash.FromToken("token").Value!, CancellationToken.None))!;

            var user = await _repository.CompleteAsync(pending, NewUser(), CancellationToken.None);

            Assert.NotNull(user);
            Assert.Empty(PendingRegistrationSingleton.Instance);
            Assert.Single(UserSingleton.Instance);
            var preferences = Assert.Single(UserPreferencesSingleton.Instance);
            Assert.Equal((Language.EN, Theme.Dark), (preferences.Language, preferences.Theme));
            Assert.Equal(12, CategorySingleton.Instance.Count);
        }

        [Fact]
        public async Task CompleteAsync_ShouldReturnNull_WhenTheSignUpWasRenewedMeanwhile()
        {
            await _repository.SaveAsync(NewPending("old"), CancellationToken.None);
            var oldPending = (await _repository.GetByTokenHashAsync(EmailTokenHash.FromToken("old").Value!, CancellationToken.None))!;
            await _repository.SaveAsync(NewPending("new"), CancellationToken.None);

            var user = await _repository.CompleteAsync(oldPending, NewUser(), CancellationToken.None);

            Assert.Null(user);
            Assert.Empty(UserSingleton.Instance);
            Assert.Single(PendingRegistrationSingleton.Instance);
        }

        [Fact]
        public async Task CompleteAsync_ShouldReturnNullAndKeepTheSignUp_WhenTheEmailAlreadyHasAnAccount()
        {
            await _repository.SaveAsync(NewPending("token"), CancellationToken.None);
            var pending = (await _repository.GetByTokenHashAsync(EmailTokenHash.FromToken("token").Value!, CancellationToken.None))!;
            await _repository.CompleteAsync(pending, NewUser(), CancellationToken.None);
            await _repository.SaveAsync(NewPending("again"), CancellationToken.None);
            var again = (await _repository.GetByTokenHashAsync(EmailTokenHash.FromToken("again").Value!, CancellationToken.None))!;

            var user = await _repository.CompleteAsync(again, NewUser(), CancellationToken.None);

            Assert.Null(user);
            Assert.Single(UserSingleton.Instance);
            Assert.Single(PendingRegistrationSingleton.Instance);
        }

        [Fact]
        public async Task DeleteExpiredBeforeAsync_ShouldDeleteOnlyTheOnesExpiredBeforeTheCutoff()
        {
            await _repository.SaveAsync(NewPending("token"), CancellationToken.None);
            var expiresAt = PendingRegistrationSingleton.Instance[0].ExpiresAt;

            Assert.Equal(0, await _repository.DeleteExpiredBeforeAsync(expiresAt, CancellationToken.None));
            Assert.Equal(1, await _repository.DeleteExpiredBeforeAsync(expiresAt.AddTicks(1), CancellationToken.None));
        }
    }
}
