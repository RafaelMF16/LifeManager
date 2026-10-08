using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits
{
    /// <summary>
    /// A habit the user wants to build (<see cref="HabitKind.Positive"/>) or to quit (<see cref="HabitKind.Negative"/>).
    /// Deleting it from the UI archives it, so its history stays; an archived habit can't change until it is restored.
    /// </summary>
    public class Habit
    {
        public HabitId? Id { get; private set; }
        public UserId UserId { get; }
        public HabitName Name { get; private set; }

        /// <summary>
        /// Persisted copy of <see cref="HabitName.NormalizedValue"/>, kept as its own column so it can be indexed
        /// (trigram search, uniqueness among active habits). Always derived from <see cref="Name"/>.
        /// </summary>
        public string NormalizedName { get; private set; }

        public HabitDescription? Description { get; private set; }
        public HabitTrigger? Trigger { get; private set; }

        /// <summary>Fixed once created: switching it would change what the habit's history means.</summary>
        public HabitKind Kind { get; }

        public HabitDifficulty Difficulty { get; private set; }

        // The frequency is stored as plain columns; Frequency rebuilds the value object from them.
        public HabitFrequencyType FrequencyType { get; private set; }
        public HabitWeekDays WeekDays { get; private set; }
        public int? TimesPerWeek { get; private set; }

        public HabitFrequency Frequency => HabitFrequency.FromPersistence(FrequencyType, WeekDays, TimesPerWeek);

        /// <summary>First day the habit counts on.</summary>
        public DateOnly StartDate { get; }

        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? ArchivedAt { get; private set; }

        public bool IsArchived => ArchivedAt is not null;

        /// <summary>In days, or in weeks for a <see cref="HabitFrequencyType.TimesPerWeek"/> habit.</summary>
        public int CurrentStreak { get; private set; }
        public int LongestStreak { get; private set; }

        /// <summary>The day-close cursor: the last day already judged (done, missed, protected or clean).</summary>
        public DateOnly EvaluatedUntil { get; private set; }

        private Habit(
            UserId userId,
            HabitName name,
            HabitDescription? description,
            HabitTrigger? trigger,
            HabitKind kind,
            HabitDifficulty difficulty,
            HabitFrequencyType frequencyType,
            HabitWeekDays weekDays,
            int? timesPerWeek,
            DateOnly startDate,
            DateTimeOffset createdAt,
            DateTimeOffset? archivedAt,
            int currentStreak,
            int longestStreak,
            DateOnly evaluatedUntil)
        {
            UserId = userId;
            Name = name;
            NormalizedName = name.NormalizedValue;
            Description = description;
            Trigger = trigger;
            Kind = kind;
            Difficulty = difficulty;
            FrequencyType = frequencyType;
            WeekDays = weekDays;
            TimesPerWeek = timesPerWeek;
            StartDate = startDate;
            CreatedAt = createdAt;
            ArchivedAt = archivedAt;
            CurrentStreak = currentStreak;
            LongestStreak = longestStreak;
            EvaluatedUntil = evaluatedUntil;
        }

        /// <param name="today">The user's current date: the habit starts counting on it.</param>
        public static Result<Habit> Create(
            int idUser,
            string? name,
            string? description,
            string? trigger,
            HabitKind kind,
            HabitDifficulty difficulty,
            HabitFrequencyType frequencyType,
            HabitWeekDays weekDays,
            int? timesPerWeek,
            DateOnly today,
            DateTimeOffset now)
        {
            if (!Enum.IsDefined(kind))
                return HabitErrors.InvalidKind;

            var valuesResult = Validate(kind, name, description, trigger, difficulty, frequencyType, weekDays, timesPerWeek);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var values = valuesResult.Value;

            return new Habit(
                new UserId(idUser),
                values.Name,
                values.Description,
                values.Trigger,
                kind,
                difficulty,
                values.Frequency.Type,
                values.Frequency.WeekDays,
                values.Frequency.TimesPerWeek,
                startDate: today,
                createdAt: now,
                archivedAt: null,
                currentStreak: 0,
                longestStreak: 0,
                evaluatedUntil: today.AddDays(-1));
        }

        /// <summary>
        /// Changes everything but the kind. A new frequency applies from the next day judged; days already judged stay
        /// as they were.
        /// </summary>
        public Result<Habit> Update(
            string? name,
            string? description,
            string? trigger,
            HabitDifficulty difficulty,
            HabitFrequencyType frequencyType,
            HabitWeekDays weekDays,
            int? timesPerWeek)
        {
            if (IsArchived)
                return HabitErrors.Archived;

            var valuesResult = Validate(Kind, name, description, trigger, difficulty, frequencyType, weekDays, timesPerWeek);
            if (!valuesResult.IsSuccess)
                return valuesResult.Error;

            var values = valuesResult.Value;

            Name = values.Name;
            NormalizedName = values.Name.NormalizedValue;
            Description = values.Description;
            Trigger = values.Trigger;
            Difficulty = difficulty;
            FrequencyType = values.Frequency.Type;
            WeekDays = values.Frequency.WeekDays;
            TimesPerWeek = values.Frequency.TimesPerWeek;

            return this;
        }

        public Result<Habit> Archive(DateTimeOffset now)
        {
            if (IsArchived)
                return HabitErrors.AlreadyArchived;

            ArchivedAt = now;
            return this;
        }

        /// <summary>
        /// Brings the habit back from <paramref name="today"/>: the days it stayed archived are never judged, and the
        /// streak starts over (the record is kept).
        /// </summary>
        public Result<Habit> Restore(DateOnly today)
        {
            if (!IsArchived)
                return HabitErrors.NotArchived;

            ArchivedAt = null;
            CurrentStreak = 0;

            var yesterday = today.AddDays(-1);
            if (EvaluatedUntil < yesterday)
                EvaluatedUntil = yesterday;

            return this;
        }

        /// <summary>Rehydrates a stored habit without re-validating it.</summary>
        internal static Habit FromPersistence(
            int id,
            int idUser,
            string name,
            string? description,
            string? trigger,
            HabitKind kind,
            HabitDifficulty difficulty,
            HabitFrequencyType frequencyType,
            HabitWeekDays weekDays,
            int? timesPerWeek,
            DateOnly startDate,
            DateTimeOffset createdAt,
            DateTimeOffset? archivedAt,
            int currentStreak,
            int longestStreak,
            DateOnly evaluatedUntil)
        {
            var habit = new Habit(
                new UserId(idUser),
                HabitName.FromPersistence(name),
                description is null ? null : HabitDescription.FromPersistence(description),
                trigger is null ? null : HabitTrigger.FromPersistence(trigger),
                kind,
                difficulty,
                frequencyType,
                weekDays,
                timesPerWeek,
                startDate,
                createdAt,
                archivedAt,
                currentStreak,
                longestStreak,
                evaluatedUntil);
            habit.AssignId(id);

            return habit;
        }

        public void AssignId(int id)
        {
            Id = new HabitId(id);
        }

        private static Result<ValidatedValues> Validate(
            HabitKind kind,
            string? name,
            string? description,
            string? trigger,
            HabitDifficulty difficulty,
            HabitFrequencyType frequencyType,
            HabitWeekDays weekDays,
            int? timesPerWeek)
        {
            var nameResult = HabitName.Create(name);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            var descriptionResult = HabitDescription.Create(description);
            if (!descriptionResult.IsSuccess)
                return descriptionResult.Error;

            var triggerResult = HabitTrigger.Create(trigger);
            if (!triggerResult.IsSuccess)
                return triggerResult.Error;

            if (!Enum.IsDefined(difficulty))
                return HabitErrors.InvalidDifficulty;

            var frequencyResult = HabitFrequency.Create(frequencyType, weekDays, timesPerWeek);
            if (!frequencyResult.IsSuccess)
                return frequencyResult.Error;

            // Every day without a relapse is a clean day, so a habit to avoid has no schedule.
            if (kind == HabitKind.Negative && frequencyResult.Value.Type != HabitFrequencyType.Daily)
                return HabitErrors.NegativeMustBeDaily;

            return new ValidatedValues(nameResult.Value, descriptionResult.Value, triggerResult.Value, frequencyResult.Value);
        }

        private record ValidatedValues(
            HabitName Name,
            HabitDescription? Description,
            HabitTrigger? Trigger,
            HabitFrequency Frequency);

        public override bool Equals(object? obj)
        {
            if (obj is not Habit other)
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
