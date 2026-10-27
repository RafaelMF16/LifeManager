using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Auth
{
    public class PasswordResetTokenRepository(LifeManagerDbContext dbContext) : IPasswordResetTokenRepository
    {
        private readonly LifeManagerDbContext _dbContext = dbContext;

        public async Task AddReplacingUnusedAsync(PasswordResetToken token, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            await _dbContext.PasswordResetTokens
                .Where(stored => stored.UserId == token.UserId && stored.UsedAt == null)
                .ExecuteDeleteAsync(cancellationToken);

            _dbContext.Add(token);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);
        }

        public Task<PasswordResetToken?> GetByTokenHashAsync(EmailTokenHash tokenHash, CancellationToken cancellationToken)
            => _dbContext.PasswordResetTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        public async Task<bool> UseAsync(PasswordResetToken token, PasswordHash newPasswordHash, DateTimeOffset now, CancellationToken cancellationToken)
        {
            await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var used = await _dbContext.PasswordResetTokens
                .Where(stored => stored.Id == token.Id && stored.UsedAt == null && stored.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters.SetProperty(stored => stored.UsedAt, now), cancellationToken);

            if (used == 0)
                return false;

            await _dbContext.Users
                .Where(user => user.Id == token.UserId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.PasswordHash, newPasswordHash), cancellationToken);

            await _dbContext.RefreshTokens
                .Where(refreshToken => refreshToken.UserId == token.UserId && !refreshToken.IsRevoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(refreshToken => refreshToken.IsRevoked, true), cancellationToken);

            await databaseTransaction.CommitAsync(cancellationToken);

            return true;
        }

        public Task<int> DeleteStaleBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => _dbContext.PasswordResetTokens
                .Where(token => token.ExpiresAt < cutoff || token.UsedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
