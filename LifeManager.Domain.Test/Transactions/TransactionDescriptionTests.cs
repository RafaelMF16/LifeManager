using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionDescriptionTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnDescriptionIsNullOrWhiteSpace_WhenValueIsMissing(string? value)
        {
            var result = TransactionDescription.Create(value!);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DescriptionIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnDescriptionTooLong_WhenValueHasMoreThanMaxLength()
        {
            var result = TransactionDescription.Create(new string('a', TransactionDescription.MaxLength + 1));

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DescriptionTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldTrimValue_WhenValueHasSurroundingSpaces()
        {
            var paddedMaxLength = $"  {new string('a', TransactionDescription.MaxLength)}  ";

            var result = TransactionDescription.Create(paddedMaxLength);

            Assert.True(result.IsSuccess);
            Assert.Equal(new string('a', TransactionDescription.MaxLength), result.Value.Value);
        }

        [Fact]
        public void Create_ShouldNormalizeValue_IgnoringCaseAndAccents()
        {
            var result = TransactionDescription.Create("Farmácia São João");

            Assert.True(result.IsSuccess);
            Assert.Equal("Farmácia São João", result.Value.Value);
            Assert.Equal("farmacia sao joao", result.Value.NormalizedValue);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = TransactionDescription.Create("description").Value!;
            var valueTwo = TransactionDescription.Create("description").Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = TransactionDescription.Create("description").Value!;
            var valueTwo = TransactionDescription.Create("description").Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
