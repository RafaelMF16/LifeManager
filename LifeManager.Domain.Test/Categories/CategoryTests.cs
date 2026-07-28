using LifeManager.Domain.Categories;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Domain.Test.Categories
{
    public class CategoryTests
    {
        [Fact]
        public void Create_ShouldReturnCategory_WhenValuesAreValid()
        {
            var userId = 1;
            var type = MoneyFlowType.Income;
            var name = "name";
            var category = Category.Create(userId, type, name);

            Assert.NotNull(category);
            Assert.IsType<Category>(category);
            Assert.Null(category.Id);
            Assert.Equal(userId, category.UserId.Value);
            Assert.Equal(type, category.Type);
            Assert.Equal(name, category.Name.Value);
        }
    }
}