using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Auth
{
    // The reset service relies on the mock behaving like the real replacement and conditional use, so it is tested on its own.
    [Collection("ApplicationServices")]
    public class PasswordResetTokenRepositoryMockTest : BaseTest
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

        private readonly IPasswordResetTokenRepository _repository;

        public PasswordResetTokenRepositoryMockTest()
        {
            PasswordResetTokenSingleton.Instance.Clear();
            RefreshTokenSingleton.Instance.Clear();
            UserSingleton.Instance.Clear();
            _repository = ServiceProvider.GetRequiredService<IPasswordResetTokenRepository>();
            Assert.IsType<PasswordResetTokenRepositoryMock>(_repository);
        }

        private static User SeedUser()
        {
            var user = User.Create("Ana", "ana@example.com", "old-hash").Value!;
            user.AssignId(1);
            UserSingleton.Instance.Add(user);
            return user;
        }

        private static PasswordResetToken NewToken(User user, string token, DateTimeOffset? now = null)
            => PasswordResetToken.Create(user.Id!, EmailTokenHash.FromToken(token).Value!, now ?? Now);

        private static EmailTokenHash HashOf(string token) => EmailTokenHash.FromToken(token).Value!;

        private static RefreshToken SeedSession(User user)
        {
            var session = RefreshToken.Create(user.Id!.Value, $"session-{user.Id.Value}", Now.AddDays(7), Now.AddDays(30), false).Value!;
            RefreshTokenSingleton.Instance.Add(session);
            return session;
        }

        [Fact]
        public async Task AddReplacingUnusedAsync_ShouldReplaceTheUsersUnusedLinks()
        {
            var user = SeedUser();
            await _repository.AddReplacingUnusedAsync(NewToken(user, "first"), CancellationToken.None);

            await _repository.AddReplacingUnusedAsync(NewToken(user, "second"), CancellationToken.None);

            Assert.Null(await _repository.GetByTokenHashAsync(HashOf("first"), CancellationToken.None));
            Assert.NotNull(await _repository.GetByTokenHashAsync(HashOf("second"), CancellationToken.None));
        }

        [Fact]
        public async Task UseAsync_ShouldMarkTheLinkUsedAndChangeThePassword()
        {
            var user = SeedUser();
            await _repository.AddReplacingUnusedAsync(NewToken(user, "token"), CancellationToken.None);
            var token = (await _repository.GetByTokenHashAsync(HashOf("token"), CancellationToken.None))!;

            var used = await _repository.UseAsync(token, PasswordHash.Create("new-hash").Value!, Now, CancellationToken.None);

            Assert.True(used);
            Assert.Equal("new-hash", user.PasswordHash.Value);
            Assert.Equal(Now, Assert.Single(PasswordResetTokenSingleton.Instance).UsedAt);
        }

        [Fact]
        public async Task UseAsync_ShouldRevokeOnlyTheUsersSessions()
        {
            var user = SeedUser();
            var other = User.Create("Bia", "bia@example.com", "hash").Value!;
            other.AssignId(2);
            UserSingleton.Instance.Add(other);
            var userSession = SeedSession(user);
            var otherSession = SeedSession(other);
            await _repository.AddReplacingUnusedAsync(NewToken(user, "token"), CancellationToken.None);
            var token = (await _repository.GetByTokenHashAsync(HashOf("token"), CancellationToken.None))!;

            await _repository.UseAsync(token, PasswordHash.Create("new-hash").Value!, Now, CancellationToken.None);

            Assert.True(userSession.IsRevoked);
            Assert.False(otherSession.IsRevoked);
        }

        [Fact]
        public async Task UseAsync_ShouldWriteNothing_WhenTheLinkWasAlreadyUsed()
        {
            var user = SeedUser();
            await _repository.AddReplacingUnusedAsync(NewToken(user, "token"), CancellationToken.None);
            var token = (await _repository.GetByTokenHashAsync(HashOf("token"), CancellationToken.None))!;
            await _repository.UseAsync(token, PasswordHash.Create("first-hash").Value!, Now, CancellationToken.None);

            var used = await _repository.UseAsync(token, PasswordHash.Create("second-hash").Value!, Now, CancellationToken.None);

            Assert.False(used);
            Assert.Equal("first-hash", user.PasswordHash.Value);
        }

        [Fact]
        public async Task UseAsync_ShouldWriteNothing_WhenTheLinkExpired()
        {
            var user = SeedUser();
            await _repository.AddReplacingUnusedAsync(NewToken(user, "token"), CancellationToken.None);
            var token = (await _repository.GetByTokenHashAsync(HashOf("token"), CancellationToken.None))!;

            var session = SeedSession(user);

            var used = await _repository.UseAsync(token, PasswordHash.Create("new-hash").Value!, Now.AddHours(PasswordResetToken.ValidityHours), CancellationToken.None);

            Assert.False(used);
            Assert.Equal("old-hash", user.PasswordHash.Value);
            Assert.False(session.IsRevoked);
        }

        [Fact]
        public async Task DeleteStaleBeforeAsync_ShouldDeleteExpiredAndUsedLinksOnly()
        {
            var user = SeedUser();
            var other = User.Create("Bia", "bia@example.com", "hash").Value!;
            other.AssignId(2);
            UserSingleton.Instance.Add(other);
            var cutoff = Now.AddDays(-PasswordResetToken.RetentionDays);
            await _repository.AddReplacingUnusedAsync(NewToken(user, "expired", now: cutoff.AddHours(-2)), CancellationToken.None);
            await _repository.AddReplacingUnusedAsync(NewToken(other, "fresh"), CancellationToken.None);

            var deleted = await _repository.DeleteStaleBeforeAsync(cutoff, CancellationToken.None);

            Assert.Equal(1, deleted);
            Assert.NotNull(await _repository.GetByTokenHashAsync(HashOf("fresh"), CancellationToken.None));
        }
    }
}
