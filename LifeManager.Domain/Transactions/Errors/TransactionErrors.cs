using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Transactions.Errors
{
    public static class TransactionErrors
    {
        public static readonly Error NotFound = Error.NotFound("Transaction.NotFound", "Transaction not found");

        public static readonly Error InvalidType = Error.Validation("Transaction.InvalidType", "Transaction type must be Expense or Income");
        public static readonly Error DescriptionIsNullOrWhiteSpace = Error.Validation("Transaction.DescriptionIsNullOrWhiteSpace", "TransactionDescription is required");
        public static readonly Error DescriptionTooLong = Error.Validation("Transaction.DescriptionTooLong", "TransactionDescription cannot be longer than 80 characters");
        public static readonly Error AmountNotPositive = Error.Validation("Transaction.AmountNotPositive", "TransactionAmount must be greater than zero");
        public static readonly Error AmountTooManyDecimals = Error.Validation("Transaction.AmountTooManyDecimals", "TransactionAmount cannot have more than 2 decimal places");
        public static readonly Error AmountTooLarge = Error.Validation("Transaction.AmountTooLarge", "TransactionAmount is too large");
        public static readonly Error DateOutsideMonth = Error.Validation("Transaction.DateOutsideMonth", "Transaction date must be inside the month of its monthly summary");
    }
}
