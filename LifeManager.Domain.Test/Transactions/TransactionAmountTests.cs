using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionAmountTests
    {
        [Theory]
        [InlineData(-1)]
        [InlineData(-10)]
        [InlineData(-100)]
        public void Create_ShouldThrowDomainException_WhenValueIsNegative(int value)
        {
            const string errorMessageExpected = $"{nameof(TransactionAmount)} cannot be negative";
            var exception = Assert.Throws<DomainException>(() => TransactionAmount.Create(value));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(55)]
        public void Create_ShouldReturnTransactionAmount_WhenValueIsValid(int value)
        {
            var amount = TransactionAmount.Create(value);
            Assert.NotNull(amount);
            Assert.IsType<TransactionAmount>(amount);
            Assert.Equal(value, amount.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TransactionAmount.Create(value);
            var valueTwo = TransactionAmount.Create(value);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const int value = 1;
            var valueOne = TransactionAmount.Create(value);
            var valueTwo = TransactionAmount.Create(value);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}