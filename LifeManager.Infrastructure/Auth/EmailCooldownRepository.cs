using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Enums;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Auth
{
    public class EmailCooldownRepository(LifeManagerDbContext context) : IEmailCooldownRepository
    {
        private readonly LifeManagerDbContext _context = context;

        /// <summary>
        /// One conditional upsert: inserts the row, or moves <c>LastSentAt</c> only when the cooldown passed. The row comes
        /// back (<c>RETURNING</c>) only when it was written, and the row lock of <c>ON CONFLICT</c> makes concurrent
        /// requests wait for each other, so only one of them gets it.
        /// </summary>
        public async Task<bool> TryReserveAsync(Email email, EmailKind kind, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var key = EmailCooldown.KeyOf(email.Value);
            var kindValue = (int)kind;
            var sendableBefore = now - EmailCooldown.Duration;

            var reserved = await _context.Database
                .SqlQuery<int>($"""
                    INSERT INTO "EmailCooldowns" ("Email", "Kind", "LastSentAt")
                    VALUES ({key}, {kindValue}, {now})
                    ON CONFLICT ("Email", "Kind") DO UPDATE SET "LastSentAt" = EXCLUDED."LastSentAt"
                    WHERE "EmailCooldowns"."LastSentAt" <= {sendableBefore}
                    RETURNING 1 AS "Value"
                    """)
                .ToListAsync(cancellationToken);

            return reserved.Count > 0;
        }

        public Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken)
            => _context.EmailCooldowns
                .Where(cooldown => cooldown.LastSentAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
    }
}
