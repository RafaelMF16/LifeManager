using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.GameItems.Errors
{
    public static class GameItemErrors
    {
        public static readonly Error PurchaseNotFound = Error.NotFound("GameItems.PurchaseNotFound", "Item purchase not found");

        public static readonly Error UnknownItem = Error.Validation("GameItems.UnknownItem", "This item is not in the shop");
        public static readonly Error UndoOutsideWindow = Error.Validation("GameItems.UndoOutsideWindow", "An item purchase can only be undone on the day it was made");

        public static readonly Error InsufficientCoins = Error.Conflict("GameItems.InsufficientCoins", "Not enough coins to buy this item");
        public static readonly Error HpFull = Error.Conflict("GameItems.HpFull", "HP is already full");
        public static readonly Error StreakFreezesFull = Error.Conflict("GameItems.StreakFreezesFull", "The player already holds the maximum number of streak freezes");
        public static readonly Error DailyLimitReached = Error.Conflict("GameItems.DailyLimitReached", "This item's daily purchase limit was reached");
        public static readonly Error WeeklyLimitReached = Error.Conflict("GameItems.WeeklyLimitReached", "This item's weekly purchase limit was reached");
        public static readonly Error AlreadyUndone = Error.Conflict("GameItems.AlreadyUndone", "This item purchase was already undone");
        public static readonly Error ItemAlreadyUsed = Error.Conflict("GameItems.ItemAlreadyUsed", "The streak freeze bought was already used");
    }
}
