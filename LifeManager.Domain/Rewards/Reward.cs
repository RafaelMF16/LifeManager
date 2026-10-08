using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Rewards
{
    /// <summary>
    /// Something the user allows themselves in exchange for the coins earned with habits ("2h of video games"). Deleting
    /// it from the UI archives it, so its redemptions keep pointing at it; an archived reward can't change or be redeemed
    /// until it is restored.
    /// </summary>
    public class Reward
    {
        public const int MinCost = 1;
        public const int MaxCost = 100_000;
        public const int IconMaxLength = 30;

        public RewardId? Id { get; private set; }
        public UserId UserId { get; }
        public RewardName Name { get; private set; }

        /// <summary>
        /// Persisted copy of <see cref="RewardName.NormalizedValue"/>, kept as its own column so it can be indexed
        /// (trigram search, uniqueness among active rewards). Always derived from <see cref="Name"/>.
        /// </summary>
        public string NormalizedName { get; private set; }

        /// <summary>In coins.</summary>
        public int Cost { get; private set; }

        /// <summary>The id of an icon the frontend knows; it falls back to its default icon for any other value.</summary>
        public string? Icon { get; private set; }

        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? ArchivedAt { get; private set; }

        public bool IsArchived => ArchivedAt is not null;

        private Reward(UserId userId, RewardName name, int cost, string? icon, DateTimeOffset createdAt, DateTimeOffset? archivedAt)
        {
            UserId = userId;
            Name = name;
            NormalizedName = name.NormalizedValue;
            Cost = cost;
            Icon = icon;
            CreatedAt = createdAt;
            ArchivedAt = archivedAt;
        }

        public static Result<Reward> Create(int idUser, string? name, int cost, string? icon, DateTimeOffset now)
        {
            var valuesResult = Validate(name, cost, icon);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var (rewardName, normalizedIcon) = valuesResult.Value;

            return new Reward(new UserId(idUser), rewardName, cost, normalizedIcon, now, archivedAt: null);
        }

        /// <summary>Rehydrates a stored reward, so tests can seed any state.</summary>
        internal static Reward FromPersistence(int id, int idUser, string name, int cost, string? icon, DateTimeOffset createdAt, DateTimeOffset? archivedAt)
        {
            var reward = new Reward(new UserId(idUser), RewardName.FromPersistence(name), cost, icon, createdAt, archivedAt);
            reward.AssignId(id);

            return reward;
        }

        /// <summary>A new cost only applies to later redemptions: each one keeps the price it was paid.</summary>
        public Result<Reward> Update(string? name, int cost, string? icon)
        {
            if (IsArchived)
                return RewardErrors.Archived;

            var valuesResult = Validate(name, cost, icon);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var (rewardName, normalizedIcon) = valuesResult.Value;

            Name = rewardName;
            NormalizedName = rewardName.NormalizedValue;
            Cost = cost;
            Icon = normalizedIcon;

            return this;
        }

        public Result<Reward> Archive(DateTimeOffset now)
        {
            if (IsArchived)
                return RewardErrors.AlreadyArchived;

            ArchivedAt = now;
            return this;
        }

        public Result<Reward> Restore()
        {
            if (!IsArchived)
                return RewardErrors.NotArchived;

            ArchivedAt = null;
            return this;
        }

        public Result EnsureCanRedeem()
            => IsArchived ? RewardErrors.Archived : Result.Success();

        public void AssignId(int id)
        {
            Id = new RewardId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Reward other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();

        /// <summary>The name, and the icon trimmed (blank as null).</summary>
        private static Result<(RewardName Name, string? Icon)> Validate(string? name, int cost, string? icon)
        {
            var nameResult = RewardName.Create(name);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            if (cost is < MinCost or > MaxCost)
                return RewardErrors.InvalidCost;

            var normalizedIcon = string.IsNullOrWhiteSpace(icon) ? null : icon.Trim();
            if (normalizedIcon is not null && normalizedIcon.Length > IconMaxLength)
                return RewardErrors.IconTooLong;

            return (nameResult.Value, normalizedIcon);
        }
    }
}
