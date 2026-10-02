using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Transactions.Errors;

namespace LifeManager.Domain.Transactions.ValueObjects
{
    public class TransactionDescription
    {
        public const short MaxLength = 80;

        public string Value { get; }

        /// <summary>Case- and accent-insensitive form, used for searching.</summary>
        public string NormalizedValue { get; }

        private TransactionDescription(string value)
        {
            Value = value;
            NormalizedValue = SearchText.Normalize(value);
        }

        public static Result<TransactionDescription> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TransactionErrors.DescriptionIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return TransactionErrors.DescriptionTooLong;

            return new TransactionDescription(trimmedValue);
        }

        internal static TransactionDescription FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is TransactionDescription other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
