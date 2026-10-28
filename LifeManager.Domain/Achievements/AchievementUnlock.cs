using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Achievements.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Achievements
{
    /// <summary>
    /// An achievement a player unlocked: once per player, never taken back. Starts unseen, so the frontend can stop the
    /// player for it once, on whichever device.
    /// </summary>
    public class AchievementUnlock
    {
        public AchievementUnlockId? Id { get; private set; }
        public UserId UserId { get; }
        public AchievementCode Code { get; }

        /// <summary>The game day it was unlocked, in the business time zone.</summary>
        public DateOnly UnlockedOn { get; }

        public DateTimeOffset UnlockedAt { get; }

        /// <summary>When the player was shown it; null until then.</summary>
        public DateTimeOffset? SeenAt { get; private set; }

        public bool IsSeen => SeenAt is not null;

        private AchievementUnlock(UserId userId, AchievementCode code, DateOnly unlockedOn, DateTimeOffset unlockedAt, DateTimeOffset? seenAt)
        {
            UserId = userId;
            Code = code;
            UnlockedOn = unlockedOn;
            UnlockedAt = unlockedAt;
            SeenAt = seenAt;
        }

        public static AchievementUnlock Create(UserId userId, AchievementCode code, DateOnly today, DateTimeOffset now)
            => new(userId, code, today, now, seenAt: null);

        /// <summary>Rehydrates a stored unlock, so tests can seed any state.</summary>
        internal static AchievementUnlock FromPersistence(int id, int idUser, AchievementCode code, DateOnly unlockedOn, DateTimeOffset unlockedAt, DateTimeOffset? seenAt)
        {
            var unlock = new AchievementUnlock(new UserId(idUser), code, unlockedOn, unlockedAt, seenAt);
            unlock.AssignId(id);

            return unlock;
        }

        /// <summary>Marks it as shown; a second call keeps the first time.</summary>
        public void MarkSeen(DateTimeOffset now)
        {
            SeenAt ??= now;
        }

        public void AssignId(int id)
        {
            Id = new AchievementUnlockId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not AchievementUnlock other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
