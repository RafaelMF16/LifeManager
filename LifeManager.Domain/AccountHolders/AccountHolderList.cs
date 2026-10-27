using LifeManager.Domain.AccountHolders.Errors;
using LifeManager.Domain.AccountHolders.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.AccountHolders
{
    /// <summary>
    /// The names of the people and companies that own the user's accounts, as the bank writes them. A statement line
    /// that transfers money to or from one of them is a transfer between the user's own accounts. Saving always
    /// replaces the whole list, so it is validated as a whole: an error about one name says which one (<see cref="Error.Index"/>).
    /// </summary>
    public sealed class AccountHolderList
    {
        public const int MaxHolders = 5;

        public UserId UserId { get; }
        public IReadOnlyList<AccountHolder> Holders { get; }

        private AccountHolderList(UserId userId, IReadOnlyList<AccountHolder> holders)
        {
            UserId = userId;
            Holders = holders;
        }

        public static Result<AccountHolderList> Create(UserId userId, IReadOnlyList<string?>? names)
        {
            if (names is null)
                return AccountHolderErrors.NamesRequired;

            if (names.Count > MaxHolders)
                return AccountHolderErrors.TooMany;

            var holders = new List<AccountHolder>(names.Count);
            var seen = new HashSet<string>();
            for (var index = 0; index < names.Count; index++)
            {
                var nameResult = AccountHolderName.Create(names[index]);
                if (!nameResult.IsSuccess)
                    return nameResult.Error.AtIndex(index);

                if (!seen.Add(nameResult.Value.NormalizedValue))
                    return AccountHolderErrors.DuplicateName.AtIndex(index);

                holders.Add(AccountHolder.For(userId, nameResult.Value, index));
            }

            return new AccountHolderList(userId, holders);
        }
    }
}
