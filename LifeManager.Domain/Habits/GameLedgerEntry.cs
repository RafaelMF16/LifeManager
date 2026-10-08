using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// One line of the player's statement: an append-only record of a change to coins, XP or HP. The deltas are the
    /// ones actually applied, so the sum of a user's entries always equals their profile (from the default one) and
    /// an undo is the exact inverse of an entry.
    /// </summary>
    public class GameLedgerEntry
    {
        public const int DescriptionMaxLength = 80;

        /// <summary>
        /// The kinds that are coins earned with habits. An <see cref="GameLedgerEntryKind.Undo"/> with a habit takes one of
        /// them back; an undo with a reward gives back a redemption, which isn't an earning.
        /// </summary>
        public static readonly IReadOnlySet<GameLedgerEntryKind> HabitEarningKinds = new HashSet<GameLedgerEntryKind>
        {
            GameLedgerEntryKind.HabitDone,
            GameLedgerEntryKind.CleanDay,
            GameLedgerEntryKind.StreakMilestone
        };

        public GameLedgerEntryId? Id { get; private set; }
        public UserId UserId { get; }
        public GameLedgerEntryKind Kind { get; }

        /// <summary>The game day the entry belongs to (e.g. the check-in's date), in the business time zone.</summary>
        public DateOnly OccurredOn { get; }

        /// <summary>When it was recorded; orders the statement.</summary>
        public DateTimeOffset CreatedAt { get; }

        public int CoinsDelta { get; }
        public int XpDelta { get; }
        public int HpDelta { get; }

        /// <summary>A copy of what caused it (a habit or reward name) as it was then, so renaming it later keeps the history.</summary>
        public string? Description { get; }

        /// <summary>The habit behind the change (check-in, miss, relapse...), when there is one.</summary>
        public HabitId? HabitId { get; }

        /// <summary>The reward behind the change (a redemption or its undo), when there is one.</summary>
        public RewardId? RewardId { get; }

        private GameLedgerEntry(
            UserId userId,
            GameLedgerEntryKind kind,
            DateOnly occurredOn,
            DateTimeOffset createdAt,
            int coinsDelta,
            int xpDelta,
            int hpDelta,
            string? description,
            HabitId? habitId,
            RewardId? rewardId)
        {
            UserId = userId;
            Kind = kind;
            OccurredOn = occurredOn;
            CreatedAt = createdAt;
            CoinsDelta = coinsDelta;
            XpDelta = xpDelta;
            HpDelta = hpDelta;
            Description = description;
            HabitId = habitId;
            RewardId = rewardId;
        }

        /// <summary>
        /// The entries for one <see cref="PlayerProfile.Apply"/>: the <paramref name="kind"/> entry with the applied
        /// delta, then a <see cref="GameLedgerEntryKind.LevelUp"/> entry when the HP was refilled by a new level and a
        /// <see cref="GameLedgerEntryKind.Knockout"/> entry when the player was knocked out. Every entry carries
        /// <paramref name="habitId"/> and <paramref name="rewardId"/>.
        /// </summary>
        public static IReadOnlyList<GameLedgerEntry> FromOutcome(
            UserId userId,
            GameLedgerEntryKind kind,
            DateOnly occurredOn,
            DateTimeOffset createdAt,
            string? description,
            GameOutcome outcome,
            HabitId? habitId = null,
            RewardId? rewardId = null)
        {
            var applied = outcome.Applied;
            var entries = new List<GameLedgerEntry>
            {
                new(userId, kind, occurredOn, createdAt, applied.Coins, applied.Xp, applied.Hp, Truncate(description), habitId, rewardId)
            };

            if (outcome.LeveledUp)
                entries.Add(new GameLedgerEntry(userId, GameLedgerEntryKind.LevelUp, occurredOn, createdAt, 0, 0, outcome.LevelUpHpRestored, null, habitId, rewardId));

            if (outcome.KnockedOut)
                entries.Add(new GameLedgerEntry(userId, GameLedgerEntryKind.Knockout, occurredOn, createdAt, -outcome.KnockoutCoinsLost, 0, outcome.KnockoutHpRestored, null, habitId, rewardId));

            return entries;
        }

        public void AssignId(int id)
        {
            Id = new GameLedgerEntryId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not GameLedgerEntry other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();

        private static string? Truncate(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return null;

            var trimmed = description.Trim();
            return trimmed.Length <= DescriptionMaxLength ? trimmed : trimmed[..DescriptionMaxLength];
        }
    }
}
