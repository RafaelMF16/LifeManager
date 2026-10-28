namespace LifeManager.Domain.Achievements.Enums
{
    /// <summary>The fixed achievements, in catalog order. Stored as its number, so never reorder or reuse the values.</summary>
    public enum AchievementCode
    {
        CheckIns10 = 1,
        CheckIns100 = 2,
        CheckIns500 = 3,
        Streak7 = 4,
        Streak30 = 5,
        Streak100 = 6,
        Level5 = 7,
        Level10 = 8,
        Level25 = 9,
        Redemptions1 = 10,
        Redemptions10 = 11,
        CleanDays10 = 12,
        CleanDays50 = 13,
        CleanDays200 = 14,
        Comeback = 15,
        AutomaticHabit = 16
    }
}
