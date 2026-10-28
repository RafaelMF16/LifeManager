using LifeManager.Application.Achievements.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Achievements;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Achievements.Interfaces;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Achievements.Services
{
    /// <summary>
    /// Unlocks the achievements the player's history reaches. Runs when the profile is read (the frontend reloads it
    /// after every change), so it needs no hook in the features that change the game and counts the past too. Each
    /// unlock pays its bonus once, in the ledger (<see cref="GameLedgerEntryKind.AchievementUnlocked"/>), and is never
    /// taken back.
    /// </summary>
    public class AchievementService(IAchievementRepository achievementRepository, AppClock appClock, TimeProvider timeProvider)
    {
        private readonly IAchievementRepository _achievementRepository = achievementRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Unlocks what's new, paying the bonuses. Reads without the lock first, so a read with nothing new costs no
        /// lock; the unlock reads everything again under it.
        /// </summary>
        /// <returns>The achievements unlocked by this call, in catalog order.</returns>
        public async Task<IReadOnlyList<AchievementUnlock>> SyncAsync(UserId userId, CancellationToken cancellationToken)
        {
            var progress = await _achievementRepository.GetProgressAsync(userId, cancellationToken);
            var unlocks = await _achievementRepository.GetUnlocksAsync(userId, cancellationToken);
            if (AchievementCatalog.NewlyUnlocked(progress, Codes(unlocks)).Count == 0)
                return [];

            var today = _appClock.Today();
            var now = _timeProvider.GetUtcNow();

            return await _achievementRepository.UnlockAsync(userId, (lockedProfile, lockedProgress, lockedUnlocks) =>
            {
                var unlocked = new HashSet<AchievementCode>(Codes(lockedUnlocks));
                var newUnlocks = new List<AchievementUnlock>();
                var entries = new List<GameLedgerEntry>();

                // A bonus's XP can reach a level achievement, so look again until nothing is new: each turn unlocks at
                // least one more code, so it stops within the catalog's size.
                var current = lockedProgress;
                for (var codes = AchievementCatalog.NewlyUnlocked(current, unlocked); codes.Count > 0; codes = AchievementCatalog.NewlyUnlocked(current, unlocked))
                {
                    foreach (var code in codes)
                    {
                        var outcome = lockedProfile.Apply(AchievementCatalog.Bonus(AchievementCatalog.Tier(code)));
                        newUnlocks.Add(AchievementUnlock.Create(userId, code, today, now));
                        entries.AddRange(GameLedgerEntry.FromOutcome(
                            userId, GameLedgerEntryKind.AchievementUnlocked, today, now, null, outcome, achievementCode: code));
                        unlocked.Add(code);
                    }

                    current = current with { Level = lockedProfile.Level };
                }

                return newUnlocks.Count == 0 ? null : new AchievementUnlockDecision(newUnlocks, entries);
            }, cancellationToken);
        }

        /// <summary>Syncs, then lists the whole catalog with the player's progress and unlock dates.</summary>
        public async Task<AchievementsResponseDto> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            await SyncAsync(userId, cancellationToken);

            var progress = await _achievementRepository.GetProgressAsync(userId, cancellationToken);
            var unlocks = (await _achievementRepository.GetUnlocksAsync(userId, cancellationToken)).ToDictionary(unlock => unlock.Code);

            var achievements = AchievementCatalog.All
                .Select(code => AchievementDto.From(code, progress, unlocks.GetValueOrDefault(code)))
                .ToList();

            return new AchievementsResponseDto(achievements, unlocks.Count, AchievementCatalog.All.Count);
        }

        /// <summary>The unlocks the player hasn't been shown yet, in catalog order.</summary>
        public async Task<IReadOnlyList<NewAchievementDto>> GetUnseenAsync(UserId userId, CancellationToken cancellationToken)
        {
            var unlocks = await _achievementRepository.GetUnlocksAsync(userId, cancellationToken);

            return [.. unlocks.Where(unlock => !unlock.IsSeen).OrderBy(unlock => unlock.Code).Select(NewAchievementDto.From)];
        }

        /// <summary>Marks every unlock as shown, so the frontend stops the player for them only once, on any device.</summary>
        public Task MarkSeenAsync(UserId userId, CancellationToken cancellationToken)
            => _achievementRepository.MarkSeenAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);

        private static IReadOnlyCollection<AchievementCode> Codes(IEnumerable<AchievementUnlock> unlocks)
            => [.. unlocks.Select(unlock => unlock.Code)];
    }
}
