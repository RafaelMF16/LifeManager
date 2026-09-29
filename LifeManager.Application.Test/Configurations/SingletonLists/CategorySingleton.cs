using LifeManager.Domain.Categories;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class CategorySingleton : List<Category>
    {
        private CategorySingleton() { }

        private static readonly Lazy<CategorySingleton> lazy = new(() => new CategorySingleton());

        public static CategorySingleton Instance => lazy.Value;
    }
}
