using LifeManager.Domain.Exceptions;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.Test.Transactions
{
    public class TransactionDescriptionTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsNull()
        {
            const string errorMessageExpected = $"{nameof(TransactionDescription)} is required";
            var exception = Assert.Throws<DomainException>(() => TransactionDescription.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(TransactionDescription)} is required";
            var exception = Assert.Throws<DomainException>(() => TransactionDescription.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnTransactionDescription_WhenValueIsValid()
        {
            const string description = "description";
            var transactionDescription = TransactionDescription.Create(description);
            Assert.NotNull(transactionDescription);
            Assert.IsType<TransactionDescription>(transactionDescription);
            Assert.Equal(description, transactionDescription.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string description = "description";
            var valueOne = TransactionDescription.Create(description);
            var valueTwo = TransactionDescription.Create(description);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string description = "description";
            var valueOne = TransactionDescription.Create(description);
            var valueTwo = TransactionDescription.Create(description);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}