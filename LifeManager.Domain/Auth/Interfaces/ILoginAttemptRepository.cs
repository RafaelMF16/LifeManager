using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Auth.Interfaces
{
    public interface ILoginAttemptRepository
    {
        Task<LoginAttempt?> GetAsync(Email email, string ipAddress, CancellationToken cancellationToken);

        /// <summary>
        /// Counts one wrong password for the pair: starts the row, adds to its window or, when the window is over, starts
        /// a new one (<see cref="LoginAttempt.RecordFailure"/>). Atomic, so concurrent failures all count.
        /// </summary>
        Task RecordFailureAsync(Email email, string ipAddress, DateTimeOffset now, CancellationToken cancellationToken);

        Task ClearAsync(Email email, string ipAddress, CancellationToken cancellationToken);

        Task<int> DeleteWindowsStartedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);
    }
}
