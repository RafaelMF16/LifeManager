using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Domain.Test.Categories
{
    public class DefaultCategoriesTests
    {
        [Fact]
        public void Names_ShouldReturnPortugueseNames_WhenLanguageIsPtBr()
        {
            var names = DefaultCategories.Names(Language.PTBR);

            Assert.Equal(
                ["Moradia", "Mercado", "Restaurantes", "Transporte", "Saúde", "Educação",
                 "Lazer", "Assinaturas", "Compras", "Salário", "Investimentos", "Outros"],
                names);
        }

        [Fact]
        public void Names_ShouldReturnEnglishNames_WhenLanguageIsEn()
        {
            var names = DefaultCategories.Names(Language.EN);

            Assert.Equal(
                ["Housing", "Groceries", "Restaurants", "Transportation", "Health", "Education",
                 "Leisure", "Subscriptions", "Shopping", "Salary", "Investments", "Other"],
                names);
        }

        [Fact]
        public void Names_ShouldReturnPortugueseNames_WhenLanguageIsUndefined()
        {
            var names = DefaultCategories.Names((Language)99);

            Assert.Equal(DefaultCategories.Names(Language.PTBR), names);
        }

        [Theory]
        [InlineData(Language.PTBR)]
        [InlineData(Language.EN)]
        public void Names_ShouldBeValidAndDistinctCategoryNames_WhenLanguageIsSupported(Language language)
        {
            var names = DefaultCategories.Names(language);

            var categoryNames = names.Select(name => CategoryName.Create(name)).ToList();

            Assert.All(categoryNames, result => Assert.True(result.IsSuccess));
            Assert.Equal(names.Count, categoryNames.Select(result => result.Value!.NormalizedValue).Distinct().Count());
        }
    }
}
