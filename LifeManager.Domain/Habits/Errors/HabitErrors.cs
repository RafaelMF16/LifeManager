using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.Errors
{
    public static class HabitErrors
    {
        public static readonly Error NotFound = Error.NotFound("Habit.NotFound", "Habit not found");

        public static readonly Error NameIsNullOrWhiteSpace = Error.Validation("Habit.NameIsNullOrWhiteSpace", "Habit name is required");
        public static readonly Error NameTooLong = Error.Validation("Habit.NameTooLong", "Habit name cannot be longer than 60 characters");
        public static readonly Error DescriptionTooLong = Error.Validation("Habit.DescriptionTooLong", "Habit description cannot be longer than 200 characters");
        public static readonly Error TriggerTooLong = Error.Validation("Habit.TriggerTooLong", "Habit trigger cannot be longer than 120 characters");
        public static readonly Error InvalidKind = Error.Validation("Habit.InvalidKind", "Habit kind is invalid");
        public static readonly Error InvalidDifficulty = Error.Validation("Habit.InvalidDifficulty", "Habit difficulty is invalid");
        public static readonly Error InvalidFrequencyType = Error.Validation("Habit.InvalidFrequencyType", "Habit frequency type is invalid");
        public static readonly Error WeekDaysRequired = Error.Validation("Habit.WeekDaysRequired", "Pick at least one day of the week");
        public static readonly Error InvalidTimesPerWeek = Error.Validation("Habit.InvalidTimesPerWeek", "Times per week must be between 1 and 6");
        public static readonly Error InvalidFrequencyCombination = Error.Validation("Habit.InvalidFrequencyCombination", "Week days and times per week only apply to their own frequency type");
        public static readonly Error NegativeMustBeDaily = Error.Validation("Habit.NegativeMustBeDaily", "A habit to avoid is always tracked daily");

        public static readonly Error NameAlreadyExists = Error.Conflict("Habit.NameAlreadyExists", "An active habit with this name already exists");
        public static readonly Error Archived = Error.Conflict("Habit.Archived", "An archived habit cannot be changed; restore it first");
        public static readonly Error AlreadyArchived = Error.Conflict("Habit.AlreadyArchived", "Habit is already archived");
        public static readonly Error NotArchived = Error.Conflict("Habit.NotArchived", "Habit is not archived");
    }
}
