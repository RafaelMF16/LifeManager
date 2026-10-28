using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// An amount logged towards a habit's daily target ("+250 ml"); a day can have several. The game never reads them:
    /// reaching the target writes the day's <see cref="HabitCheckIn"/>, which is what counts (see <see cref="HabitProgress"/>).
    /// </summary>
    public class HabitProgressEntry
    {
        public const int MaxValue = 100_000;

        public HabitProgressEntryId? Id { get; private set; }
        public HabitId HabitId { get; }
        public UserId UserId { get; }
        public DateOnly Date { get; }
        public int Value { get; }
        public DateTimeOffset CreatedAt { get; }

        private HabitProgressEntry(HabitId habitId, UserId userId, DateOnly date, int value, DateTimeOffset createdAt)
        {
            HabitId = habitId;
            UserId = userId;
            Date = date;
            Value = value;
            CreatedAt = createdAt;
        }

        /// <remarks>Whether the habit takes progress on <paramref name="date"/> is <see cref="Habit.EnsureCanRecordProgress"/>'s job.</remarks>
        public static Result<HabitProgressEntry> Create(Habit habit, DateOnly date, int value, DateTimeOffset createdAt)
        {
            if (value is < 1 or > MaxValue)
                return HabitErrors.InvalidProgressValue;

            return new HabitProgressEntry(habit.Id!, habit.UserId, date, value, createdAt);
        }

        internal static HabitProgressEntry FromPersistence(int id, int idHabit, int idUser, DateOnly date, int value, DateTimeOffset createdAt)
        {
            var entry = new HabitProgressEntry(new HabitId(idHabit), new UserId(idUser), date, value, createdAt);
            entry.AssignId(id);

            return entry;
        }

        public void AssignId(int id)
        {
            Id = new HabitProgressEntryId(id);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not HabitProgressEntry other)
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
