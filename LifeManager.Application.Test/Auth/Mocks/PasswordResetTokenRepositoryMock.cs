using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Auth.Mocks
{
    public class PasswordResetTokenRepositoryMock : IPasswordResetTokenRepository
    {
        private readonly PasswordResetTokenSingleton _instance = PasswordResetTokenSingleton.Instance;

        public Task AddReplacingUnusedAsync(PasswordResetToken token, CancellationToken cancellationToken)
        {
            _instance.RemoveAll(stored => stored.UserId == token.UserId && stored.UsedAt is null);
            token.AssignId(_instance.Count == 0 ? 1 : _instance.Max(stored => stored.Id!.Value) + 1);
            _instance.Add(token);
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> GetByTokenHashAsync(EmailTokenHash tokenHash, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(token => token.TokenHash.Equals(tokenHash)));

        // Mirrors the transaction: the conditional update on the link, then the user's password and sessions; a lost race
        // writes nothing.
        public Task<bool> UseAsync(PasswordResetToken token, PasswordHash newPasswordHash, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(candidate => candidate.Id == token.Id);
            if (stored is null || !stored.IsUsable(now))
                return Task.FromResult(false);

            stored.MarkUsed(now);
            UserSingleton.Instance.SingleOrDefault(user => user.Id == token.UserId)?.ChangePassword(newPasswordHash);
            foreach (var refreshToken in RefreshTokenSingleton.Instance.Where(refreshToken => refreshToken.UserId.Equals(token.UserId) && !refreshToken.IsRevoked))
                refreshToken.RevokeToken();

            return Task.FromResult(true);
        }

        public Task<int> DeleteStaleBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => Task.FromResult(_instance.RemoveAll(token => token.ExpiresAt < cutoff || token.UsedAt < cutoff));
    }
}
