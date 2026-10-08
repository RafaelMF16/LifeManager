using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Rewards.Errors
{
    public static class RewardErrors
    {
        public static readonly Error NotFound = Error.NotFound("Rewards.NotFound", "Reward not found");
        public static readonly Error RedemptionNotFound = Error.NotFound("Rewards.RedemptionNotFound", "Redemption not found");

        public static readonly Error NameIsNullOrWhiteSpace = Error.Validation("Rewards.NameIsNullOrWhiteSpace", "Reward name is required");
        public static readonly Error NameTooLong = Error.Validation("Rewards.NameTooLong", "Reward name cannot be longer than 60 characters");
        public static readonly Error InvalidCost = Error.Validation("Rewards.InvalidCost", "Reward cost must be between 1 and 100000 coins");
        public static readonly Error IconTooLong = Error.Validation("Rewards.IconTooLong", "Reward icon cannot be longer than 30 characters");
        public static readonly Error UndoOutsideWindow = Error.Validation("Rewards.UndoOutsideWindow", "A redemption can only be undone on the day it was made");

        public static readonly Error NameAlreadyExists = Error.Conflict("Rewards.NameAlreadyExists", "An active reward with this name already exists");
        public static readonly Error Archived = Error.Conflict("Rewards.Archived", "An archived reward cannot be changed or redeemed; restore it first");
        public static readonly Error AlreadyArchived = Error.Conflict("Rewards.AlreadyArchived", "Reward is already archived");
        public static readonly Error NotArchived = Error.Conflict("Rewards.NotArchived", "Reward is not archived");
        public static readonly Error InsufficientCoins = Error.Conflict("Rewards.InsufficientCoins", "Not enough coins to redeem this reward");
        public static readonly Error AlreadyUndone = Error.Conflict("Rewards.AlreadyUndone", "This redemption was already undone");
    }
}
