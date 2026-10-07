using LifeManager.Domain.Habits;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Habits
{
    /// <summary>
    /// Keeps a player's profile equal to the sum of their ledger. Every change runs, inside one database transaction
    /// opened by the caller, as: <see cref="LockAsync"/>, <see cref="PlayerProfile.Apply"/>, <see cref="SaveAsync"/>.
    /// The lock serializes concurrent changes for the same user (a check-in and the day-close job, say), so none is
    /// lost. Repositories that write something else in the same transaction (check-ins) call these directly.
    /// </summary>
    internal static class PlayerWallet
    {
        /// <summary>
        /// Creates the user's profile if it doesn't exist yet, then locks it (SELECT ... FOR UPDATE) until the caller's
        /// database transaction ends.
        /// </summary>
        public static async Task<PlayerProfile> LockAsync(LifeManagerDbContext dbContext, UserId userId, CancellationToken cancellationToken)
        {
            // ON CONFLICT on the unique UserId index makes a concurrent first change a no-op instead of an error,
            // without aborting the surrounding transaction the way a caught unique violation would.
            var newProfile = PlayerProfile.CreateDefault(userId);
            var id = userId.Value;
            var level = newProfile.Level;
            var totalXp = newProfile.TotalXp;
            var hp = newProfile.Hp;
            var maxHp = newProfile.MaxHp;
            var coins = newProfile.Coins;
            var streakFreezes = newProfile.StreakFreezes;

            await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "PlayerProfiles" ("UserId", "Level", "TotalXp", "Hp", "MaxHp", "Coins", "StreakFreezes")
                VALUES ({id}, {level}, {totalXp}, {hp}, {maxHp}, {coins}, {streakFreezes})
                ON CONFLICT ("UserId") DO NOTHING
                """, cancellationToken);

            var lockedProfiles = await dbContext.PlayerProfiles
                .FromSql($"""SELECT * FROM "PlayerProfiles" WHERE "UserId" = {id} FOR UPDATE""")
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            return lockedProfiles.Single();
        }

        /// <summary>Appends <paramref name="entries"/> to the ledger and stores the locked profile's new values.</summary>
        public static async Task SaveAsync(LifeManagerDbContext dbContext, PlayerProfile lockedProfile, IReadOnlyList<GameLedgerEntry> entries, CancellationToken cancellationToken)
        {
            dbContext.GameLedgerEntries.AddRange(entries);
            await dbContext.SaveChangesAsync(cancellationToken);

            await dbContext.PlayerProfiles
                .Where(storedProfile => storedProfile.Id == lockedProfile.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(storedProfile => storedProfile.Level, lockedProfile.Level)
                    .SetProperty(storedProfile => storedProfile.TotalXp, lockedProfile.TotalXp)
                    .SetProperty(storedProfile => storedProfile.Hp, lockedProfile.Hp)
                    .SetProperty(storedProfile => storedProfile.MaxHp, lockedProfile.MaxHp)
                    .SetProperty(storedProfile => storedProfile.Coins, lockedProfile.Coins)
                    .SetProperty(storedProfile => storedProfile.StreakFreezes, lockedProfile.StreakFreezes), cancellationToken);
        }
    }
}
