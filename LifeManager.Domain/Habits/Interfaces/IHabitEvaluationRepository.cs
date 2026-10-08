using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Habits.Interfaces
{
    /// <summary>What the rules see while a closed day is judged, under the profile's lock.</summary>
    /// <param name="Profile">The locked profile; the rules change it (damage, reward, a freeze).</param>
    /// <param name="WeekCheckIns">The habit's check-ins in the judged day's week (Monday to Sunday).</param>
    /// <param name="SuccessDates">The habit's days that count for the streak so far.</param>
    /// <param name="FailedDates">The habit's relapse days.</param>
    public record HabitEvaluationContext(
        PlayerProfile Profile,
        IReadOnlyList<HabitCheckIn> WeekCheckIns,
        IReadOnlySet<DateOnly> SuccessDates,
        IReadOnlySet<DateOnly> FailedDates);

    /// <summary>What the judgement writes.</summary>
    /// <param name="NewCheckIns">Missed, frozen or clean days, on days that have no check-in.</param>
    /// <param name="CurrentStreak">The recalculated streak; null leaves it as it is.</param>
    public record HabitEvaluationEffects(
        IReadOnlyList<HabitCheckIn> NewCheckIns,
        IReadOnlyList<GameLedgerEntry> Entries,
        int? CurrentStreak,
        bool KnockedOut)
    {
        public static readonly HabitEvaluationEffects None = new([], [], null, false);
    }

    /// <remarks>
    /// The day close's writes are conditional on the habit's cursor (<c>EvaluatedUntil</c>): a day is judged only by the
    /// run that moves the cursor onto it, so two runs (or API instances) never judge it twice.
    /// </remarks>
    public interface IHabitEvaluationRepository
    {
        /// <summary>Users with an active habit whose cursor is before <paramref name="lastClosedDay"/>.</summary>
        Task<IReadOnlyList<UserId>> GetUserIdsToEvaluateAsync(DateOnly lastClosedDay, int limit, CancellationToken cancellationToken);

        /// <summary>The user's active habits whose cursor is before <paramref name="lastClosedDay"/>.</summary>
        Task<IReadOnlyList<Habit>> GetHabitsToEvaluateAsync(UserId userId, DateOnly lastClosedDay, CancellationToken cancellationToken);

        /// <summary>
        /// In one database transaction: locks the profile, moves the habit's cursor from the day before
        /// <paramref name="date"/> onto it (only if it is still there and the habit is active), calls
        /// <paramref name="decide"/>, and stores its check-ins, ledger entries, profile and streak.
        /// </summary>
        /// <returns>Null, with nothing written, when the cursor had already moved or the habit was archived.</returns>
        Task<HabitEvaluationEffects?> EvaluateDayAsync(Habit habit, DateOnly date, Func<HabitEvaluationContext, HabitEvaluationEffects> decide, CancellationToken cancellationToken);
    }
}
