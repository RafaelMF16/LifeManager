using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;

namespace LifeManager.Domain.Habits.Challenges
{
    /// <summary>Pure progress of a challenge, always recounted from the saved check-ins and progress entries.</summary>
    public static class ChallengeProgress
    {
        /// <summary>Days done in the week. A frozen day saves the streak, not the challenge.</summary>
        public static int Frequency(DateOnly weekStart, IEnumerable<(DateOnly Date, HabitCheckInStatus Status)> checkIns)
            => checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Done && InWeek(weekStart, checkIn.Date));

        /// <summary>Everything logged in the week, days short of the daily target included.</summary>
        public static int Volume(DateOnly weekStart, IEnumerable<(DateOnly Date, int Value)> entries)
            => entries.Where(entry => InWeek(weekStart, entry.Date)).Sum(entry => entry.Value);

        /// <summary>Days left to play, today included: the whole week before it starts, none after it.</summary>
        public static int DaysLeft(DateOnly weekStart, DateOnly today)
        {
            if (today < weekStart)
                return GameWeek.Days;

            return Math.Max(0, GameWeek.End(weekStart).DayNumber - today.DayNumber + 1);
        }

        /// <summary>No day of the week can change anymore (from Tuesday on): an active challenge has failed.</summary>
        public static bool IsOver(DateOnly weekStart, DateOnly today)
            => GameWeek.IsSettled(weekStart, today);

        private static bool InWeek(DateOnly weekStart, DateOnly date)
            => date >= weekStart && date <= GameWeek.End(weekStart);
    }
}
