using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categories
{
    public class Category
    {
        public CategoryId Id { get; }
        public UserId UserId { get; }
        public MoneyFlowType Type { get; private set; }
        public CategoryName Name { get; private set; }

        private Category(CategoryId id, UserId userId, MoneyFlowType type, CategoryName categoryName)
        {
            Id = id;
            UserId = userId;
            Type = type;
            Name = categoryName;
        }

        public static Category Create(int id, int idUser, MoneyFlowType type, string name)
        {
            var categoryId = new CategoryId(id);
            var userId = new UserId(idUser);
            var categoryName = CategoryName.Create(name);

            return new Category(categoryId, userId, type, categoryName);
        }
    }
}