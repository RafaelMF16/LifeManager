using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.Errors
{
    public static class PlayerProfileErrors
    {
        public static readonly Error StreakFreezesFull = Error.Conflict("PlayerProfile.StreakFreezesFull", "The player already holds the maximum number of streak freezes");
        public static readonly Error NoStreakFreeze = Error.Conflict("PlayerProfile.NoStreakFreeze", "The player has no streak freeze to use");
    }
}
