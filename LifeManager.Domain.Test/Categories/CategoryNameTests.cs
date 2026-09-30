using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.ValueObjects;

namespace LifeManager.Domain.Test.Categories
{
    public class CategoryNameTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnFailure_WhenValueIsNullOrWhiteSpace(string? value)
        {
            var result = CategoryName.Create(value!);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenValueIsLongerThanMaxLength()
        {
            var longName = new string('a', CategoryName.MaxLength + 1);

            var result = CategoryName.Create(longName);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnCategoryName_WhenValueHasExactlyMaxLength()
        {
            var name = new string('a', CategoryName.MaxLength);

            var result = CategoryName.Create(name);

            Assert.True(result.IsSuccess);
            Assert.Equal(name, result.Value.Value);
        }

        [Fact]
        public void Create_ShouldReturnCategoryName_WhenValueIsValid()
        {
            const string name = "Food";

            var result = CategoryName.Create(name);

            Assert.True(result.IsSuccess);
            Assert.Equal(name, result.Value.Value);
        }

        [Fact]
        public void Create_ShouldTrimValue_WhenValueHasSurroundingWhiteSpace()
        {
            var result = CategoryName.Create("  Food  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Value);
        }

        [Fact]
        public void Create_ShouldNotCountSurroundingWhiteSpace_WhenValidatingMaxLength()
        {
            var name = $"  {new string('a', CategoryName.MaxLength)}  ";

            var result = CategoryName.Create(name);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Create_ShouldExposeNormalizedValue_WithoutCaseOrAccents()
        {
            var result = CategoryName.Create("  Saúde  ");

            Assert.True(result.IsSuccess);
            Assert.Equal("Saúde", result.Value.Value);
            Assert.Equal("saude", result.Value.NormalizedValue);
        }

        [Fact]
        public void Equals_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = CategoryName.Create("Food").Value!;
            var valueTwo = CategoryName.Create("Food").Value!;

            Assert.True(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void Equals_ShouldNotBeEqual_WhenValuesAreDifferent()
        {
            var valueOne = CategoryName.Create("Food").Value!;
            var valueTwo = CategoryName.Create("Health").Value!;

            Assert.False(valueOne.Equals(valueTwo));
        }

        [Fact]
        public void GetHashCode_ShouldBeEqual_WhenValuesAreEquals()
        {
            var valueOne = CategoryName.Create("Food").Value!;
            var valueTwo = CategoryName.Create("Food").Value!;

            Assert.Equal(valueOne.GetHashCode(), valueTwo.GetHashCode());
        }
    }
}
