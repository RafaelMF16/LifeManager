using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Auth
{
    public class LoginAttemptRepository(LifeManagerDbContext context) : ILoginAttemptRepository
    {
        private readonly LifeManagerDbContext _context = context;

        public Task<LoginAttempt?> GetAsync(Email email, string ipAddress, CancellationToken cancellationToken)
        {
            var key = LoginAttempt.KeyOf(email.Value);

            return _context.LoginAttempts
                .AsNoTracking()
                .SingleOrDefaultAsync(attempt => attempt.Email == key && attempt.IpAddress == ipAddress, cancellationToken);
        }

        /// <summary>
        /// One upsert with <see cref="LoginAttempt.RecordFailure"/>'s rule in SQL: a window that started at or before
        /// <c>now - Window</c> starts over at 1, otherwise the count goes up. The row lock of <c>ON CONFLICT</c> makes
        /// concurrent failures wait for each other, so none is lost.
        /// </summary>
        public async Task RecordFailureAsync(Email email, string ipAddress, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var key = LoginAttempt.KeyOf(email.Value);
            var windowOverAtOrBefore = now - LoginAttempt.Window;

            await _context.Database.ExecuteSqlAsync($"""
                INSERT INTO "LoginAttempts" ("Email", "IpAddress", "FailedCount", "WindowStartedAt")
                VALUES ({key}, {ipAddress}, 1, {now})
                ON CONFLICT ("Email", "IpAddress") DO UPDATE SET
                    "FailedCount" = CASE WHEN "LoginAttempts"."WindowStartedAt" <= {windowOverAtOrBefore}
                        THEN 1 ELSE "LoginAttempts"."FailedCount" + 1 END,
                    "WindowStartedAt" = CASE WHEN "LoginAttempts"."WindowStartedAt" <= {windowOverAtOrBefore}
                        THEN EXCLUDED."WindowStartedAt" ELSE "LoginAttempts"."WindowStartedAt" END
                """, cancellationToken);
        }

        public Task ClearAsync(Email email, string ipAddress, CancellationToken cancellationToken)
        {
            var key = LoginAttempt.KeyOf(email.Value);

            return _context.LoginAttempts
                .Where(attempt => attempt.Email == key && attempt.IpAddress == ipAddress)
                .ExecuteDeleteAsync(cancellationToken);
        }

        public Task<int> DeleteWindowsStartedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => _context.LoginAttempts
                .Where(attempt => attempt.WindowStartedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
