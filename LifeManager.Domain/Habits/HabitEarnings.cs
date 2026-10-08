namespace LifeManager.Domain.Habits
{
    /// <summary>Coins earned with habits over some days (undone check-ins subtracted), and the first day with an earning.</summary>
    public record HabitEarnings(int Coins, DateOnly? FirstDay)
    {
        public static readonly HabitEarnings None = new(0, null);
    }
}
