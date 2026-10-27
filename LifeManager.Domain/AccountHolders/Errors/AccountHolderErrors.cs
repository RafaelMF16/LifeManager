using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.AccountHolders.Errors
{
    public static class AccountHolderErrors
    {
        public static readonly Error NamesRequired = Error.Validation("AccountHolder.NamesRequired", "The list of names is required");
        public static readonly Error TooMany = Error.Validation("AccountHolder.TooMany", $"There can be at most {AccountHolderList.MaxHolders} account holders");
        public static readonly Error NameIsNullOrWhiteSpace = Error.Validation("AccountHolder.NameIsNullOrWhiteSpace", "Name cannot be empty");
        public static readonly Error NameTooLong = Error.Validation("AccountHolder.NameTooLong", $"Name cannot have more than {ValueObjects.AccountHolderName.MaxLength} characters");
        public static readonly Error NameTooShort = Error.Validation("AccountHolder.NameTooShort", "Name must have at least two name words, as written on the bank statement");
        public static readonly Error DuplicateName = Error.Validation("AccountHolder.DuplicateName", "The same name appears more than once");
    }
}
