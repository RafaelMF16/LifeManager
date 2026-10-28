using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    /// <summary>What the rules see while progress is being logged or removed, under the profile's lock.</summary>
    /// <param name="Entry">The entry being logged, or the stored one being removed (its date is the day changed).</param>
    /// <param name="DayEntries">The day's entries before the change, oldest first.</param>
    /// <param name="DayCheckIn">The day's done check-in; null while the day isn't done.</param>
    /// <param name="SuccessDates">The habit's done, clean or protected days, as stored (the day included when done).</param>
    /// <param name="FailedDates">The habit's relapse days.</param>
    public record HabitProgressContext(
        PlayerProfile Profile,
        HabitProgressEntry Entry,
        IReadOnlyList<HabitProgressEntry> DayEntries,
        HabitCheckIn? DayCheckIn,
        IReadOnlySet<DateOnly> SuccessDates,
        IReadOnlySet<DateOnly> FailedDates);

    /// <summary>What the rules decided about the day's check-in.</summary>
    /// <param name="CheckIn">
    /// For <see cref="HabitProgressDecision.Complete"/>, the check-in to write; for <see cref="HabitProgressDecision.Undo"/>,
    /// what undoing it applied; null for <see cref="HabitProgressDecision.None"/>.
    /// </param>
    public record HabitProgressEffects(HabitProgressDecision Decision, HabitCheckInEffects? CheckIn)
    {
        public static readonly HabitProgressEffects None = new(HabitProgressDecision.None, null);
    }

    /// <param name="DayEntries">The day's entries after the change, oldest first.</param>
    public record HabitProgressSaved(HabitProgressEffects Effects, IReadOnlyList<HabitProgressEntry> DayEntries);

    /// <remarks>
    /// Logging and removing progress take the profile's lock first (the same lock as check-ins), so the day's total, its
    /// check-in and the streak are always read from up-to-date history.
    /// </remarks>
    public interface IHabitProgressRepository
    {
        /// <summary>The user's entries (every habit) from <paramref name="from"/> to <paramref name="to"/>, inclusive, oldest first.</summary>
        Task<IReadOnlyList<HabitProgressEntry>> GetByUserIdAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

        /// <summary>One habit's total per day from <paramref name="from"/> to <paramref name="to"/>, inclusive; days without entries are left out.</summary>
        Task<IReadOnlyDictionary<DateOnly, int>> GetDailyTotalsAsync(HabitId habitId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: locks the profile, reads the day, calls <paramref name="decide"/> and, when it
        /// succeeds, stores <paramref name="entry"/> with the check-in it completes (or undoes), the ledger entries, the
        /// profile and the habit's streak.
        /// </summary>
        /// <returns><paramref name="decide"/>'s error, with nothing written.</returns>
        Task<Result<HabitProgressSaved>> AddAsync(
            Habit habit,
            HabitProgressEntry entry,
            Func<HabitProgressContext, Result<HabitProgressEffects>> decide,
            CancellationToken cancellationToken);

        /// <summary>The same transaction for removing one of <paramref name="habit"/>'s entries.</summary>
        /// <returns>
        /// <see cref="Errors.HabitErrors.ProgressEntryNotFound"/> when the habit has no such entry, or
        /// <paramref name="decide"/>'s error; nothing is written either way.
        /// </returns>
        Task<Result<HabitProgressSaved>> RemoveAsync(
            Habit habit,
            HabitProgressEntryId entryId,
            Func<HabitProgressContext, Result<HabitProgressEffects>> decide,
            CancellationToken cancellationToken);
    }
}
