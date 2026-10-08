using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    /// <summary>What the rules see while a check-in or relapse (or its undo) is being written, under the profile's lock.</summary>
    /// <param name="Profile">The locked profile; the rules change it through <see cref="PlayerProfile.Apply"/>.</param>
    /// <param name="SuccessDates">The habit's done, clean or protected days, already including (or no longer including) this one.</param>
    /// <param name="FailedDates">The habit's relapse days, already including (or no longer including) this one.</param>
    /// <param name="Removed">The day being undone; null when recording one.</param>
    public record HabitCheckInContext(
        PlayerProfile Profile,
        IReadOnlySet<DateOnly> SuccessDates,
        IReadOnlySet<DateOnly> FailedDates,
        HabitCheckIn? Removed);

    /// <summary>What the rules decided: the streak to store, what was applied to the profile and the ledger lines.</summary>
    /// <param name="Applied">What the profile actually got; stored on a new day so it can be undone exactly.</param>
    /// <param name="FreezeAwarded">Whether the day earned a streak freeze; stored so its undo takes it back.</param>
    public record HabitCheckInEffects(int CurrentStreak, GameDelta Applied, GameOutcome Outcome, IReadOnlyList<GameLedgerEntry> Entries, bool FreezeAwarded = false);

    /// <remarks>
    /// The user's own days (check-ins of a habit to build, relapses of a habit to avoid) and their undos lock the
    /// user's profile first (the same lock as every other change to the game), so two of them for the same user never
    /// interleave and the streak is always worked out from up-to-date history.
    /// </remarks>
    public interface IHabitCheckInRepository
    {
        /// <summary>The user's check-ins (any status) from <paramref name="from"/> to <paramref name="to"/>, inclusive.</summary>
        Task<IReadOnlyList<HabitCheckIn>> GetByUserIdAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: locks the profile, checks the day is free, calls <paramref name="decide"/> and
        /// stores a <paramref name="status"/> day (<see cref="HabitCheckInStatus.Done"/> or
        /// <see cref="HabitCheckInStatus.Relapse"/>) with what it applied, the habit's streak, the ledger entries and the
        /// profile.
        /// </summary>
        /// <returns>Null, with nothing written, when the habit already has a day on <paramref name="date"/>.</returns>
        Task<HabitCheckInEffects?> RecordAsync(
            Habit habit,
            DateOnly date,
            HabitCheckInStatus status,
            DateTimeOffset createdAt,
            Func<HabitCheckInContext, HabitCheckInEffects> decide,
            CancellationToken cancellationToken);

        /// <summary>The same transaction for an undo: deletes the <paramref name="status"/> day, then stores what <paramref name="decide"/> returns.</summary>
        /// <returns>Null, with nothing written, when the habit has no <paramref name="status"/> day on <paramref name="date"/>.</returns>
        Task<HabitCheckInEffects?> RemoveAsync(
            Habit habit,
            DateOnly date,
            HabitCheckInStatus status,
            Func<HabitCheckInContext, HabitCheckInEffects> decide,
            CancellationToken cancellationToken);
    }
}
