using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// What happened to a habit on one day; at most one per habit and date. A <see cref="HabitCheckInStatus.Done"/>
    /// check-in keeps what it awarded, so undoing it gives back exactly that.
    /// </summary>
    public class HabitCheckIn
    {
        public HabitCheckInId? Id { get; private set; }
        public HabitId HabitId { get; }
        public UserId UserId { get; }
        public DateOnly Date { get; }
        public HabitCheckInStatus Status { get; }
        public DateTimeOffset CreatedAt { get; }

        /// <summary>The coins, XP and HP actually applied to the profile for this day (after the limits).</summary>
        public int CoinsAwarded { get; }
        public int XpAwarded { get; }
        public int HpAwarded { get; }

        public GameDelta Awarded => new(CoinsAwarded, XpAwarded, HpAwarded);

        /// <summary>Whether this day earned the player a streak freeze, so undoing it takes the freeze back.</summary>
        public bool FreezeAwarded { get; }

        private HabitCheckIn(
            HabitId habitId,
            UserId userId,
            DateOnly date,
            HabitCheckInStatus status,
            DateTimeOffset createdAt,
            int coinsAwarded,
            int xpAwarded,
            int hpAwarded,
            bool freezeAwarded)
        {
            HabitId = habitId;
            UserId = userId;
            Date = date;
            Status = status;
            CreatedAt = createdAt;
            CoinsAwarded = coinsAwarded;
            XpAwarded = xpAwarded;
            HpAwarded = hpAwarded;
            FreezeAwarded = freezeAwarded;
        }

        /// <param name="awarded">The delta <see cref="PlayerProfile.Apply"/> actually applied (reward and milestone).</param>
        public static HabitCheckIn Done(Habit habit, DateOnly date, DateTimeOffset createdAt, GameDelta awarded, bool freezeAwarded = false)
            => Judged(habit, date, HabitCheckInStatus.Done, createdAt, awarded, freezeAwarded);

        /// <summary>A day the day close judged: missed, protected by a freeze, or clean.</summary>
        /// <param name="applied">What the judgement did to the profile (damage, a clean day's reward, or nothing).</param>
        public static HabitCheckIn Judged(Habit habit, DateOnly date, HabitCheckInStatus status, DateTimeOffset createdAt, GameDelta applied, bool freezeAwarded = false)
            => new(habit.Id!, habit.UserId, date, status, createdAt, applied.Coins, applied.Xp, applied.Hp, freezeAwarded);

        internal static HabitCheckIn FromPersistence(
            int id,
            int idHabit,
            int idUser,
            DateOnly date,
            HabitCheckInStatus status,
            DateTimeOffset createdAt,
            int coinsAwarded,
            int xpAwarded,
            int hpAwarded,
            bool freezeAwarded = false)
        {
            var checkIn = new HabitCheckIn(new HabitId(idHabit), new UserId(idUser), date, status, createdAt, coinsAwarded, xpAwarded, hpAwarded, freezeAwarded);
            checkIn.AssignId(id);

            return checkIn;
        }

        /// <summary>The statuses that count for the streak: done, protected by a freeze, or clean (a habit to avoid).</summary>
        public static readonly IReadOnlyList<HabitCheckInStatus> SuccessStatuses =
            [HabitCheckInStatus.Done, HabitCheckInStatus.Frozen, HabitCheckInStatus.Clean];

        /// <summary>Whether the day counts for the streak (<see cref="SuccessStatuses"/>).</summary>
        public bool IsSuccess => SuccessStatuses.Contains(Status);

        public void AssignId(int id)
        {
            Id = new HabitCheckInId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not HabitCheckIn other)
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
