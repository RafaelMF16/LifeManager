using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionAmountTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-0.01)]
        public void Create_ShouldReturnAmountNotPositive_WhenValueIsZeroOrNegative(decimal value)
        {
            var result = TransactionAmount.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.AmountNotPositive, result.Error);
        }

        [Theory]
        [InlineData(0.001)]
        [InlineData(10.555)]
        public void Create_ShouldReturnAmountTooManyDecimals_WhenValueHasMoreThanTwoDecimalPlaces(decimal value)
        {
            var result = TransactionAmount.Create(value);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.AmountTooManyDecimals, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnAmountTooLarge_WhenValueDoesNotFitTheColumn()
        {
            var result = TransactionAmount.Create(TransactionAmount.MaxValue + 0.01m);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.AmountTooLarge, result.Error);
        }

        [Theory]
        [InlineData(0.01)]
        [InlineData(10.5)]
        [InlineData(55.55)]
        [InlineData(999_999_999_999.99)]
        public void Create_ShouldReturnTransactionAmount_WhenValueIsValid(decimal value)
        {
            var result = TransactionAmount.Create(value);

            Assert.True(result.IsSuccess);
            Assert.Equal(value, result.Value.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = TransactionAmount.Create(1m).Value!;
            var valueTwo = TransactionAmount.Create(1m).Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = TransactionAmount.Create(1m).Value!;
            var valueTwo = TransactionAmount.Create(1m).Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
