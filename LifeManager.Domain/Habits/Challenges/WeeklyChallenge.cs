using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Challenges
{
    /// <summary>
    /// An optional goal for one habit to build in one Monday-to-Sunday week. Its baseline, effort and reward are fixed
    /// when it is created, so a later change in the history never changes what it pays. Completing it pays once and is
    /// never undone; failing costs nothing.
    /// </summary>
    public class WeeklyChallenge
    {
        public WeeklyChallengeId? Id { get; private set; }
        public UserId UserId { get; }
        public HabitId HabitId { get; }
        public DateOnly WeekStart { get; }
        public ChallengeType Type { get; }
        public int Target { get; }

        /// <summary>The weekly average of the closed weeks before it; null without any.</summary>
        public decimal? Baseline { get; }

        public ChallengeEffort Effort { get; }
        public int RewardCoins { get; }
        public int RewardXp { get; }
        public ChallengeStatus Status { get; private set; }
        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? CompletedAt { get; private set; }

        /// <summary>When the player saw it completed (the celebration), on any device.</summary>
        public DateTimeOffset? SeenAt { get; private set; }

        private WeeklyChallenge(
            UserId userId, HabitId habitId, DateOnly weekStart, ChallengeType type, int target, decimal? baseline,
            ChallengeEffort effort, int rewardCoins, int rewardXp, ChallengeStatus status, DateTimeOffset createdAt,
            DateTimeOffset? completedAt, DateTimeOffset? seenAt)
        {
            UserId = userId;
            HabitId = habitId;
            WeekStart = weekStart;
            Type = type;
            Target = target;
            Baseline = baseline;
            Effort = effort;
            RewardCoins = rewardCoins;
            RewardXp = rewardXp;
            Status = status;
            CreatedAt = createdAt;
            CompletedAt = completedAt;
            SeenAt = seenAt;
        }

        /// <summary>
        /// Checks everything that depends on the habit and the week; the per-habit and per-week limits need the stored
        /// challenges and are the caller's.
        /// </summary>
        public static Result<WeeklyChallenge> Create(ChallengeDraft draft, DateTimeOffset createdAt)
        {
            var habit = draft.Habit;

            if (!Enum.IsDefined(draft.Type))
                return ChallengeErrors.InvalidType;
            if (habit.IsArchived || habit.Kind != HabitKind.Positive)
                return ChallengeErrors.HabitNotEligible;
            if (draft.Type == ChallengeType.Volume && habit.Target is null)
                return ChallengeErrors.NotMeasurable;

            var window = ChallengeRules.EnsureWeekIsOpen(draft.WeekStart, draft.Today);
            if (!window.IsSuccess)
                return window.Error!;

            var max = draft.Type == ChallengeType.Frequency
                ? ChallengeRules.MaxFrequencyTarget(habit, draft.WeekStart, draft.Today, draft.DoneDates)
                : ChallengeRules.MaxVolumeTarget(habit);
            if (draft.Target < 1 || draft.Target > max)
                return ChallengeErrors.InvalidTarget;
            if (draft.Target <= draft.Progress)
                return ChallengeErrors.TargetAlreadyReached;

            var effort = ChallengeRules.Effort(draft.Type, draft.Target, draft.Baseline);
            var reward = ChallengeRules.Reward(habit.Difficulty, effort);

            return new WeeklyChallenge(
                habit.UserId, habit.Id!, draft.WeekStart, draft.Type, draft.Target, draft.Baseline, effort, reward.Coins,
                reward.Xp, ChallengeStatus.Active, createdAt, null, null);
        }

        public Result Complete(DateTimeOffset completedAt)
        {
            if (Status != ChallengeStatus.Active)
                return ChallengeErrors.NotActive;

            Status = ChallengeStatus.Completed;
            CompletedAt = completedAt;

            return Result.Success();
        }

        public Result Fail()
        {
            if (Status != ChallengeStatus.Active)
                return ChallengeErrors.NotActive;

            Status = ChallengeStatus.Failed;

            return Result.Success();
        }

        public Result Cancel()
        {
            if (Status != ChallengeStatus.Active)
                return ChallengeErrors.NotActive;

            Status = ChallengeStatus.Cancelled;

            return Result.Success();
        }

        /// <summary>Only an active challenge can be given up (deleted); a finished one is history.</summary>
        public Result EnsureCanDelete()
            => Status == ChallengeStatus.Active ? Result.Success() : ChallengeErrors.NotActive;

        public void MarkSeen(DateTimeOffset seenAt)
        {
            SeenAt ??= seenAt;
        }

        public void AssignId(int id)
        {
            Id = new WeeklyChallengeId(id);
        }

        internal static WeeklyChallenge FromPersistence(
            int id, int userId, int habitId, DateOnly weekStart, ChallengeType type, int target, decimal? baseline,
            ChallengeEffort effort, int rewardCoins, int rewardXp, ChallengeStatus status, DateTimeOffset createdAt,
            DateTimeOffset? completedAt, DateTimeOffset? seenAt)
            => new(new UserId(userId), new HabitId(habitId), weekStart, type, target, baseline, effort, rewardCoins, rewardXp,
                status, createdAt, completedAt, seenAt)
            {
                Id = new WeeklyChallengeId(id)
            };
    }
}
