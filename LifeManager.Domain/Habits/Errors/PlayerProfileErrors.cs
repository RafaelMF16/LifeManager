using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.Errors
{
    public static class PlayerProfileErrors
    {
        public static readonly Error StreakFreezesFull = Error.Conflict("PlayerProfile.StreakFreezesFull", "The player already holds the maximum number of streak freezes");
        public static readonly Error NoStreakFreeze = Error.Conflict("PlayerProfile.NoStreakFreeze", "The player has no streak freeze to use");
        public static readonly Error UnknownAvatar = Error.Validation("PlayerProfile.UnknownAvatar", "The avatar is not one of the fixed avatars");
        public static readonly Error UnknownEvolutionRange = Error.Validation("PlayerProfile.UnknownEvolutionRange", "The range is not Month, Quarter or Year");
        public static readonly Error TitleLocked = Error.Validation("PlayerProfile.TitleLocked", "The title belongs to an achievement the player hasn't unlocked");
    }
}
