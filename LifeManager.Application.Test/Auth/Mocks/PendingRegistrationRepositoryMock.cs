using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Auth.Mocks
{
    public class PendingRegistrationRepositoryMock : IPendingRegistrationRepository
    {
        private readonly PendingRegistrationSingleton _instance = PendingRegistrationSingleton.Instance;

        public Task<PendingRegistration?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(pending => pending.Email.Value == email.Value));

        public Task<PendingRegistration?> GetByTokenHashAsync(EmailTokenHash tokenHash, CancellationToken cancellationToken)
            => Task.FromResult(_instance.SingleOrDefault(pending => pending.TokenHash.Equals(tokenHash)));

        // Mirrors the upsert on the unique e-mail: the stored row keeps its id and takes the new values.
        public Task SaveAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(pending => pending.Email.Value == pendingRegistration.Email.Value);
            if (stored is not null)
            {
                _instance.Remove(stored);
                pendingRegistration.AssignId(stored.Id!.Value);
            }
            else
            {
                pendingRegistration.AssignId(_instance.Count == 0 ? 1 : _instance.Max(pending => pending.Id!.Value) + 1);
            }

            _instance.Add(pendingRegistration);
            return Task.CompletedTask;
        }

        // Mirrors the transaction: conditional delete on id + token, then the account; a taken e-mail writes nothing.
        public async Task<User?> CompleteAsync(PendingRegistration pendingRegistration, User user, CancellationToken cancellationToken)
        {
            var stored = _instance.SingleOrDefault(pending =>
                pending.Id == pendingRegistration.Id && pending.TokenHash.Equals(pendingRegistration.TokenHash));
            if (stored is null)
                return null;

            if (UserSingleton.Instance.Any(existing => existing.Email.Value == user.Email.Value))
                return null;

            _instance.Remove(stored);

            return await new UserRepositoryMock().AddWithStarterDataAsync(user, pendingRegistration.Theme, pendingRegistration.Language, cancellationToken);
        }

        public Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => Task.FromResult(_instance.RemoveAll(pending => pending.ExpiresAt < cutoff));
    }
}
