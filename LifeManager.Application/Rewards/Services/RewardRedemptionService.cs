using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Rewards.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Rewards.Errors;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.Rewards.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Rewards.Services
{
    /// <summary>
    /// Spending coins on rewards. A redemption pays the reward's current price and is written in the ledger; it can be
    /// undone (the coins come back, with an inverse ledger entry) only on the day it was made.
    /// </summary>
    public class RewardRedemptionService(
        IRewardRepository rewardRepository,
        IRewardRedemptionRepository rewardRedemptionRepository,
        IGameLedgerRepository gameLedgerRepository,
        IMonthlySummaryRepository monthlySummaryRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IRewardRepository _rewardRepository = rewardRepository;
        private readonly IRewardRedemptionRepository _rewardRedemptionRepository = rewardRedemptionRepository;
        private readonly IGameLedgerRepository _gameLedgerRepository = gameLedgerRepository;
        private readonly IMonthlySummaryRepository _monthlySummaryRepository = monthlySummaryRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Pays the reward's price; with <see cref="RewardRedeemDto.RegisterExpense"/>, also posts its expense into
        /// today's month (opened if missing), in the same database transaction.
        /// </summary>
        public async Task<Result<RewardRedemptionResultDto>> RedeemAsync(
            int rewardId,
            RewardRedeemDto? redeemDto,
            UserId userId,
            CancellationToken cancellationToken)
        {
            var reward = await _rewardRepository.GetByIdAsync(new RewardId(rewardId), userId, cancellationToken);
            if (reward is null)
                return RewardErrors.NotFound;

            var registerExpense = redeemDto?.RegisterExpense ?? false;
            if (registerExpense && !reward.HasExpense)
                return RewardErrors.ExpenseNotConfigured;

            var today = _appClock.Today();
            var now = _timeProvider.GetUtcNow();

            var redemptionResult = RewardRedemption.Create(reward, today, now);
            if (!redemptionResult.IsSuccess)
                return redemptionResult.Error;

            var redemption = redemptionResult.Value;

            Transaction? expense = null;
            if (registerExpense)
            {
                // An archived reward fails above, so the month is only opened for a redemption that can happen.
                var month = await _monthlySummaryRepository.AddIfMissingAsync(
                    MonthlySummary.OpenForRecurringPosting(userId, YearMonth.From(today)), cancellationToken);

                var expenseResult = Transaction.CreateFromRedemption(reward, today, month);
                if (!expenseResult.IsSuccess)
                    return expenseResult.Error;

                expense = expenseResult.Value;
            }

            GameOutcome? outcome = null;

            // The balance is checked inside the profile's lock: two redemptions at once can't spend the same coins.
            var profile = await _rewardRedemptionRepository.RedeemAsync(redemption, expense, lockedProfile =>
            {
                if (lockedProfile.Coins < redemption.CostPaid)
                    return null;

                outcome = lockedProfile.Apply(new GameDelta(-redemption.CostPaid, 0, 0));
                return GameLedgerEntry.FromOutcome(
                    userId, GameLedgerEntryKind.RewardRedeemed, today, now, redemption.RewardName, outcome, rewardId: reward.Id);
            }, cancellationToken);

            if (profile is null)
                return RewardErrors.InsufficientCoins;

            return new RewardRedemptionResultDto(
                RewardRedemptionResponseDto.From(redemption, today), WalletChangeDto.From(profile, outcome!), expense?.MonthlySummaryId.Value);
        }

        /// <summary>Gives the price paid back, the same day only, and deletes the expense it posted (if still there).</summary>
        public async Task<Result<RewardRedemptionResultDto>> UndoAsync(int redemptionId, UserId userId, CancellationToken cancellationToken)
        {
            var redemption = await _rewardRedemptionRepository.GetByIdAsync(new RewardRedemptionId(redemptionId), userId, cancellationToken);
            if (redemption is null)
                return RewardErrors.RedemptionNotFound;

            var today = _appClock.Today();
            var now = _timeProvider.GetUtcNow();

            var undoResult = redemption.Undo(today, now);
            if (!undoResult.IsSuccess)
                return undoResult.Error;

            GameOutcome? outcome = null;

            var undone = await _rewardRedemptionRepository.UndoAsync(redemption, lockedProfile =>
            {
                outcome = lockedProfile.Apply(new GameDelta(redemption.CostPaid, 0, 0));
                return GameLedgerEntry.FromOutcome(
                    userId, GameLedgerEntryKind.Undo, today, now, redemption.RewardName, outcome, rewardId: redemption.RewardId);
            }, cancellationToken);

            if (undone is null)
                return RewardErrors.AlreadyUndone;

            return new RewardRedemptionResultDto(
                RewardRedemptionResponseDto.From(redemption, today), WalletChangeDto.From(undone.Profile, outcome!), undone.RemovedExpenseMonthId?.Value);
        }

        public async Task<Result<PagedResponseDto<RewardRedemptionResponseDto>>> GetPagedAsync(
            RewardRedemptionListQueryDto query,
            UserId userId,
            CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var today = _appClock.Today();
            var redemptions = await _rewardRedemptionRepository.GetPagedByUserIdAsync(userId, pageRequestResult.Value, cancellationToken);

            return PagedResponseDto<RewardRedemptionResponseDto>.From(redemptions, redemption => RewardRedemptionResponseDto.From(redemption, today));
        }

        public async Task<EarningPaceDto> GetEarningPaceAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var earnings = await _gameLedgerRepository.GetHabitEarningsAsync(userId, RewardPace.WindowStart(today), today, cancellationToken);

            return new EarningPaceDto(RewardPace.AverageDailyCoins(earnings.Coins, earnings.FirstDay, today), RewardPace.WindowDays);
        }
    }
}
