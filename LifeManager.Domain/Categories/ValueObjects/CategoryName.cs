using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Categories.ValueObjects
{
    public class CategoryName
    {
        public const short MaxLength = 50;

        public string Value { get; }

        private CategoryName(string value)
        {
            Value = value;
        }

        public static Result<CategoryName> Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return CategoryErrors.NameIsNullOrWhiteSpace;

            var trimmedValue = value.Trim();
            if (trimmedValue.Length > MaxLength)
                return CategoryErrors.NameTooLong;

            return new CategoryName(trimmedValue);
        }

        internal static CategoryName FromPersistence(string value) => new(value);

        public override bool Equals(object? obj)
        {
            if (obj is CategoryName other)
                return Value == other.Value;

            return false;
        }

        public override int GetHashCode()
            => Value.GetHashCode();
    }
}
