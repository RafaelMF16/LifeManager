namespace LifeManager.Domain.GameItems.Enums
{
    /// <summary>Whether the player can buy an item now and, if not, the first reason in the purchase's check order.</summary>
    public enum GameItemAvailability
    {
        Available = 1,
        LimitReached = 2,
        HpFull = 3,
        StreakFreezesFull = 4,
        InsufficientCoins = 5
    }
}
