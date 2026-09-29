using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;

namespace LifeManager.Domain.Test.Categories
{
    public class CategoryTests
    {
        [Fact]
        public void Create_ShouldReturnCategory_WhenValuesAreValid()
        {
            const int userId = 1;
            const string name = "Food";

            var result = Category.Create(userId, name);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.Id);
            Assert.Equal(userId, result.Value.UserId.Value);
            Assert.Equal(name, result.Value.Name.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_ShouldReturnFailure_WhenNameIsNullOrWhiteSpace(string? name)
        {
            var result = Category.Create(1, name!);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnFailure_WhenNameIsTooLong()
        {
            var result = Category.Create(1, new string('a', 51));

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameTooLong, result.Error);
        }

        [Fact]
        public void Rename_ShouldChangeName_WhenNameIsValid()
        {
            var category = Category.Create(1, "Food").Value!;

            var result = category.Rename("Groceries");

            Assert.True(result.IsSuccess);
            Assert.Same(category, result.Value);
            Assert.Equal("Groceries", category.Name.Value);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Rename_ShouldKeepOriginalName_WhenNameIsNullOrWhiteSpace(string? name)
        {
            var category = Category.Create(1, "Food").Value!;

            var result = category.Rename(name!);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
            Assert.Equal("Food", category.Name.Value);
        }

        [Fact]
        public void Rename_ShouldKeepOriginalName_WhenNameIsTooLong()
        {
            var category = Category.Create(1, "Food").Value!;

            var result = category.Rename(new string('a', 51));

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameTooLong, result.Error);
            Assert.Equal("Food", category.Name.Value);
        }

        [Fact]
        public void AssignId_ShouldSetId_WhenCategoryHasNoIdYet()
        {
            var category = Category.Create(1, "Food").Value!;

            category.AssignId(10);

            Assert.NotNull(category.Id);
            Assert.Equal(10, category.Id!.Value);
        }

        [Fact]
        public void Equals_ShouldReturnTrue_WhenCategoriesHaveTheSameId()
        {
            var category1 = Category.Create(1, "Food").Value!;
            var category2 = Category.Create(2, "Health").Value!;
            category1.AssignId(1);
            category2.AssignId(1);

            Assert.Equal(category1, category2);
            Assert.Equal(category1.GetHashCode(), category2.GetHashCode());
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenCategoriesHaveDifferentIds()
        {
            var category1 = Category.Create(1, "Food").Value!;
            var category2 = Category.Create(1, "Food").Value!;
            category1.AssignId(1);
            category2.AssignId(2);

            Assert.NotEqual(category1, category2);
        }

        [Fact]
        public void Equals_ShouldReturnFalse_WhenNeitherCategoryHasBeenAssignedAnId()
        {
            var category1 = Category.Create(1, "Food").Value!;
            var category2 = Category.Create(1, "Food").Value!;

            Assert.NotEqual(category1, category2);
            Assert.Equal(category1, category1);
        }
    }
}
