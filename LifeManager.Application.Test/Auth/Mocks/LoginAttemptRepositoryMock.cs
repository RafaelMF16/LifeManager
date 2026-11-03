using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Auth.Mocks
{
    /// <summary>Mirrors the upsert: insert when missing, otherwise count or start a new window (the domain's rule).</summary>
    public class LoginAttemptRepositoryMock : ILoginAttemptRepository
    {
        private readonly LoginAttemptSingleton _instance = LoginAttemptSingleton.Instance;

        public Task<LoginAttempt?> GetAsync(Email email, string ipAddress, CancellationToken cancellationToken)
            => Task.FromResult(Find(email, ipAddress));

        public Task RecordFailureAsync(Email email, string ipAddress, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var attempt = Find(email, ipAddress);
            if (attempt is null)
                _instance.Add(LoginAttempt.Create(email.Value, ipAddress, now));
            else
                attempt.RecordFailure(now);

            return Task.CompletedTask;
        }

        public Task ClearAsync(Email email, string ipAddress, CancellationToken cancellationToken)
        {
            var key = LoginAttempt.KeyOf(email.Value);
            _instance.RemoveAll(attempt => attempt.Email == key && attempt.IpAddress == ipAddress);
            return Task.CompletedTask;
        }

        public Task<int> DeleteWindowsStartedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => Task.FromResult(_instance.RemoveAll(attempt => attempt.WindowStartedAt < cutoff));

        private LoginAttempt? Find(Email email, string ipAddress)
        {
            var key = LoginAttempt.KeyOf(email.Value);
            return _instance.Find(attempt => attempt.Email == key && attempt.IpAddress == ipAddress);
        }
    }
}
