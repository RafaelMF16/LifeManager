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

        private HabitCheckIn(
            HabitId habitId,
            UserId userId,
            DateOnly date,
            HabitCheckInStatus status,
            DateTimeOffset createdAt,
            int coinsAwarded,
            int xpAwarded,
            int hpAwarded)
        {
            HabitId = habitId;
            UserId = userId;
            Date = date;
            Status = status;
            CreatedAt = createdAt;
            CoinsAwarded = coinsAwarded;
            XpAwarded = xpAwarded;
            HpAwarded = hpAwarded;
        }

        /// <param name="awarded">The delta <see cref="PlayerProfile.Apply"/> actually applied.</param>
        public static HabitCheckIn Done(Habit habit, DateOnly date, DateTimeOffset createdAt, GameDelta awarded)
            => new(habit.Id!, habit.UserId, date, HabitCheckInStatus.Done, createdAt, awarded.Coins, awarded.Xp, awarded.Hp);

        internal static HabitCheckIn FromPersistence(
            int id,
            int idHabit,
            int idUser,
            DateOnly date,
            HabitCheckInStatus status,
            DateTimeOffset createdAt,
            int coinsAwarded,
            int xpAwarded,
            int hpAwarded)
        {
            var checkIn = new HabitCheckIn(new HabitId(idHabit), new UserId(idUser), date, status, createdAt, coinsAwarded, xpAwarded, hpAwarded);
            checkIn.AssignId(id);

            return checkIn;
        }

        /// <summary>Whether the day counts for the streak.</summary>
        public bool IsSuccess => Status is HabitCheckInStatus.Done or HabitCheckInStatus.Frozen;

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
