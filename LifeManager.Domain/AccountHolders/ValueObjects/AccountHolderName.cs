using LifeManager.Domain.AccountHolders.Errors;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.StatementImports;

namespace LifeManager.Domain.AccountHolders.ValueObjects
{
    /// <summary>
    /// The name of an account holder as the bank writes it. It needs the <see cref="OwnTransferDetector.MinNameWords"/>
    /// name words the detector looks for, counted the same way, so a saved holder is never silently ignored.
    /// </summary>
    public class AccountHolderName
    {
        public const short MaxLength = 100;

        public string Value { get; }

        /// <summary>Case-, accent- and spacing-insensitive form, used to find a name listed twice.</summary>
        public string NormalizedValue { get; }

        private AccountHolderName(string value)
        {
            Value = value;
            // "José  da Silva" is the same holder as "Jose da Silva": runs of spaces collapse to one.
            NormalizedValue = string.Join(' ', SearchText.Normalize(value).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        public static Result<AccountHolderName> Create(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return AccountHolderErrors.NameIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return AccountHolderErrors.NameTooLong;

            if (OwnTransferDetector.CountNameWords(trimmedValue) < OwnTransferDetector.MinNameWords)
                return AccountHolderErrors.NameTooShort;

            return new AccountHolderName(trimmedValue);
        }

        internal static AccountHolderName FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is AccountHolderName other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
