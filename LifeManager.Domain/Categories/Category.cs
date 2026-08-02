using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.Categories
{
    public class Category
    {
        public CategoryId? Id { get; private set; }
        public UserId UserId { get; }
        public MoneyFlowType Type { get; private set; }
        public CategoryName Name { get; private set; }

        private Category(UserId userId, MoneyFlowType type, CategoryName categoryName)
        {
            UserId = userId;
            Type = type;
            Name = categoryName;
        }

        public static Category Create(int idUser, MoneyFlowType type, string name)
        {
            var userId = new UserId(idUser);
            var categoryName = CategoryName.Create(name);

            return new Category(userId, type, categoryName);
        }

        public void AssignId(int id)
        {
            Id = new CategoryId(id);
        }
    }
}