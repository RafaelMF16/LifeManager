using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Rewards
{
    /// <summary>
    /// One purchase of a reward. Keeps a copy of the reward's name, icon and price as they were, so editing the reward
    /// later doesn't rewrite the history. It can be undone (the coins come back) only on the day it was made.
    /// </summary>
    public class RewardRedemption
    {
        public RewardRedemptionId? Id { get; private set; }
        public RewardId RewardId { get; }
        public UserId UserId { get; }
        public string RewardName { get; }
        public string? RewardIcon { get; }

        /// <summary>The price paid, in coins.</summary>
        public int CostPaid { get; }

        /// <summary>The game day it was made, in the business time zone; the undo window.</summary>
        public DateOnly RedeemedOn { get; }

        /// <summary>When it was made; orders the history.</summary>
        public DateTimeOffset RedeemedAt { get; }

        public DateTimeOffset? UndoneAt { get; private set; }

        /// <summary>
        /// The expense the redemption posted, when the user chose to register it; undoing the redemption deletes it.
        /// Null again when the user deletes that transaction by hand, which keeps the redemption.
        /// </summary>
        public TransactionId? TransactionId { get; private set; }

        public bool IsUndone => UndoneAt is not null;

        private RewardRedemption(
            RewardId rewardId,
            UserId userId,
            string rewardName,
            string? rewardIcon,
            int costPaid,
            DateOnly redeemedOn,
            DateTimeOffset redeemedAt,
            DateTimeOffset? undoneAt)
        {
            RewardId = rewardId;
            UserId = userId;
            RewardName = rewardName;
            RewardIcon = rewardIcon;
            CostPaid = costPaid;
            RedeemedOn = redeemedOn;
            RedeemedAt = redeemedAt;
            UndoneAt = undoneAt;
        }

        /// <summary>A redemption of a stored, active reward at its current price.</summary>
        public static Result<RewardRedemption> Create(Reward reward, DateOnly today, DateTimeOffset now)
        {
            var canRedeem = reward.EnsureCanRedeem();
            if (!canRedeem.IsSuccess)
                return canRedeem.Error;

            return new RewardRedemption(reward.Id!, reward.UserId, reward.Name.Value, reward.Icon, reward.Cost, today, now, undoneAt: null);
        }

        /// <summary>Rehydrates a stored redemption, so tests can seed any state.</summary>
        internal static RewardRedemption FromPersistence(
            int id,
            int idReward,
            int idUser,
            string rewardName,
            string? rewardIcon,
            int costPaid,
            DateOnly redeemedOn,
            DateTimeOffset redeemedAt,
            DateTimeOffset? undoneAt,
            int? idTransaction = null)
        {
            var redemption = new RewardRedemption(
                new RewardId(idReward), new UserId(idUser), rewardName, rewardIcon, costPaid, redeemedOn, redeemedAt, undoneAt);
            redemption.AssignId(id);
            redemption.TransactionId = idTransaction is null ? null : new TransactionId(idTransaction.Value);

            return redemption;
        }

        public bool CanUndo(DateOnly today)
            => !IsUndone && RedeemedOn == today;

        /// <summary>Whether it can be undone on <paramref name="today"/>: not undone yet, and made today.</summary>
        public Result EnsureCanUndo(DateOnly today)
        {
            if (IsUndone)
                return RewardErrors.AlreadyUndone;

            if (RedeemedOn != today)
                return RewardErrors.UndoOutsideWindow;

            return Result.Success();
        }

        public Result Undo(DateOnly today, DateTimeOffset now)
        {
            var canUndo = EnsureCanUndo(today);
            if (!canUndo.IsSuccess)
                return canUndo;

            UndoneAt = now;
            return Result.Success();
        }

        /// <summary>Links the expense posted with the redemption, once it has its id.</summary>
        public void AttachExpense(TransactionId transactionId)
        {
            TransactionId = transactionId;
        }

        public void AssignId(int id)
        {
            Id = new RewardRedemptionId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not RewardRedemption other)
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
