namespace LifeManager.Domain.Habits
{
    /// <summary>A change to the player's coins, XP and HP. Positive values add, negative values take away.</summary>
    public record GameDelta(int Coins, int Xp, int Hp)
    {
        public static readonly GameDelta None = new(0, 0, 0);

        public GameDelta Inverse() => new(-Coins, -Xp, -Hp);
    }
}
