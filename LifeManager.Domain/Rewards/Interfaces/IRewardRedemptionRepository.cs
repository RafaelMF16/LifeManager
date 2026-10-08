using LifeManager.Domain.Habits;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Rewards.Interfaces
{
    /// <remarks>
    /// Redeeming and undoing lock the user's profile first (the same lock as every other change to the game), so the
    /// balance checked is the balance spent: two redemptions at once can't spend the same coins.
    /// </remarks>
    public interface IRewardRedemptionRepository
    {
        Task<RewardRedemption?> GetByIdAsync(RewardRedemptionId redemptionId, UserId userId, CancellationToken cancellationToken);

        /// <summary>The user's redemptions, newest first.</summary>
        Task<PagedList<RewardRedemption>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: locks the profile and calls <paramref name="decide"/>, which pays for the
        /// redemption and returns the ledger entries, or null to refuse it (not enough coins). Then stores the
        /// redemption, the entries and the profile.
        /// </summary>
        /// <returns>The profile after paying; null, with nothing written, when <paramref name="decide"/> refused.</returns>
        Task<PlayerProfile?> RedeemAsync(
            RewardRedemption redemption,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>?> decide,
            CancellationToken cancellationToken);

        /// <summary>
        /// The same transaction for an undo: locks the profile, stores the <paramref name="redemption"/>'s
        /// <see cref="RewardRedemption.UndoneAt"/> if it wasn't undone meanwhile, and saves what <paramref name="apply"/>
        /// returns.
        /// </summary>
        /// <returns>The profile after the refund; null, with nothing written, when it was already undone.</returns>
        Task<PlayerProfile?> UndoAsync(
            RewardRedemption redemption,
            Func<PlayerProfile, IReadOnlyList<GameLedgerEntry>> apply,
            CancellationToken cancellationToken);
    }
}
