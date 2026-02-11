using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Exceptions;

namespace LifeManager.Domain.Test.Categories
{
    public class CategoryNameTests
    {
        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsNull()
        {
            const string errorMessageExpected = $"{nameof(CategoryName)} is required";
            var exception = Assert.Throws<DomainException>(() => CategoryName.Create(null!));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldThrowDomainException_WhenValueIsEmpty()
        {
            const string errorMessageExpected = $"{nameof(CategoryName)} is required";
            var exception = Assert.Throws<DomainException>(() => CategoryName.Create(string.Empty));
            Assert.Equal(errorMessageExpected, exception.Message);
        }

        [Fact]
        public void Create_ShouldReturnCategoryName_WhenValueIsValid()
        {
            const string description = "name";
            var categoryName = CategoryName.Create(description);
            Assert.NotNull(categoryName);
            Assert.IsType<CategoryName>(categoryName);
            Assert.Equal(description, categoryName.Value);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string categoryName = "name";
            var valueOne = CategoryName.Create(categoryName);
            var valueTwo = CategoryName.Create(categoryName);
            var result = valueOne.Equals(valueTwo);

            Assert.True(result);
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            const string categoryName = "name";
            var valueOne = CategoryName.Create(categoryName);
            var valueTwo = CategoryName.Create(categoryName);

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}