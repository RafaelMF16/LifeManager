using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LifeManager.Infrastructure.Auth
{
    public class PendingRegistrationRepository(LifeManagerDbContext dbContext) : IPendingRegistrationRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public Task<PendingRegistration?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
            => _dbContext.PendingRegistrations
                .AsNoTracking()
                .SingleOrDefaultAsync(pending => pending.Email == email, cancellationToken);

        public Task<PendingRegistration?> GetByTokenHashAsync(EmailTokenHash tokenHash, CancellationToken cancellationToken)
            => _dbContext.PendingRegistrations
                .AsNoTracking()
                .SingleOrDefaultAsync(pending => pending.TokenHash == tokenHash, cancellationToken);

        public Task SaveAsync(PendingRegistration pendingRegistration, CancellationToken cancellationToken)
        {
            var name = pendingRegistration.Name.Value;
            var email = pendingRegistration.Email.Value;
            var language = (int)pendingRegistration.Language;
            var theme = (int)pendingRegistration.Theme;
            var tokenHash = pendingRegistration.TokenHash.Value;

            return _dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "PendingRegistrations" ("Name", "Email", "Language", "Theme", "TokenHash", "CreatedAt", "ExpiresAt")
                VALUES ({name}, {email}, {language}, {theme}, {tokenHash}, {pendingRegistration.CreatedAt}, {pendingRegistration.ExpiresAt})
                ON CONFLICT ("Email") DO UPDATE SET
                    "Name" = EXCLUDED."Name",
                    "Language" = EXCLUDED."Language",
                    "Theme" = EXCLUDED."Theme",
                    "TokenHash" = EXCLUDED."TokenHash",
                    "ExpiresAt" = EXCLUDED."ExpiresAt"
                """, cancellationToken);
        }

        public async Task<User?> CompleteAsync(PendingRegistration pendingRegistration, User user, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            // Conditional on the token: a second use of the link, or a link renewed meanwhile, deletes nothing.
            var deleted = await _dbContext.PendingRegistrations
                .Where(pending => pending.Id == pendingRegistration.Id && pending.TokenHash == pendingRegistration.TokenHash)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted == 0)
                return null;

            try
            {
                await UserStarterData.AddAsync(_dbContext, user, pendingRegistration.Theme, pendingRegistration.Language, cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // The e-mail got an account meanwhile (unique index on Users.Email); the transaction rolls back on dispose.
                return null;
            }

            await databaseTransaction.CommitAsync(cancellationToken);

            return user;
        }

        public Task<int> DeleteExpiredBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => _dbContext.PendingRegistrations
                .Where(pending => pending.ExpiresAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
