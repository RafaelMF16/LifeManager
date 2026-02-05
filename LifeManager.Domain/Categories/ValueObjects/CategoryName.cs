using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Categories.ValueObjects
{
    public class CategoryName
    {
        public string Value { get; }

        private CategoryName(string value)
        {
            Value = value;
        }

        public static CategoryName Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{nameof(CategoryName)} is required");

            return new CategoryName(value);
        }

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