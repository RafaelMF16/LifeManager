using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits
{
    /// <summary>What logging or removing progress does to the day's check-in.</summary>
    public enum HabitProgressDecision
    {
        /// <summary>The check-in stays as it is.</summary>
        None = 1,

        /// <summary>The day reached its target: write the <see cref="Enums.HabitCheckInStatus.Done"/> check-in and its reward.</summary>
        Complete = 2,

        /// <summary>The day fell below its target: undo the check-in, giving back what it earned.</summary>
        Undo = 3
    }

    /// <summary>
    /// How progress turns into the day's check-in. Pure: the services call it under the profile's lock with the day's
    /// total after the change. Only logging completes a day and only removing undoes one, so a new target (rule: it
    /// applies from the next change) or a day done before the habit had a target never flips the other way.
    /// </summary>
    public static class HabitProgress
    {
        public const int MaxEntriesPerDay = 50;

        /// <param name="total">The day's total, the new entry included.</param>
        /// <param name="done">Whether the day already has its done check-in.</param>
        public static HabitProgressDecision ForAdded(HabitTarget target, int total, bool done)
            => !done && total >= target.Value ? HabitProgressDecision.Complete : HabitProgressDecision.None;

        /// <param name="total">The day's total, the removed entry left out.</param>
        /// <param name="done">Whether the day has its done check-in.</param>
        public static HabitProgressDecision ForRemoved(HabitTarget target, int total, bool done)
            => done && total < target.Value ? HabitProgressDecision.Undo : HabitProgressDecision.None;

        /// <param name="dayEntryCount">The day's entries before the new one.</param>
        public static Result EnsureCanAdd(int dayEntryCount)
            => dayEntryCount >= MaxEntriesPerDay ? HabitErrors.TooManyProgressEntries : Result.Success();
    }
}
