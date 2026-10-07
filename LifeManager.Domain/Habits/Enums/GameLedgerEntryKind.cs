namespace LifeManager.Domain.Habits.Enums
{
    /// <summary>Why the player's profile changed. Stored as its number, so never reorder or reuse the values.</summary>
    public enum GameLedgerEntryKind
    {
        HabitDone = 1,
        HabitMissed = 2,
        Relapse = 3,
        CleanDay = 4,
        StreakMilestone = 5,
        RewardRedeemed = 6,
        Knockout = 7,
        Undo = 8,
        FreezeUsed = 9,
        LevelUp = 10
    }
}
