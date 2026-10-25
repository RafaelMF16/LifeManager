using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;

namespace LifeManager.Domain.Categories
{
    /// <summary>
    /// Categories every new user starts with, in the language chosen at sign-up. They are copied into the user's own
    /// categories, so renaming or deleting them later is the same as for any other category.
    /// </summary>
    public static class DefaultCategories
    {
        private static readonly string[] PortugueseNames =
        [
            "Moradia", "Mercado", "Restaurantes", "Transporte", "Saúde", "Educação",
            "Lazer", "Assinaturas", "Compras", "Salário", "Investimentos", "Outros"
        ];

        private static readonly string[] EnglishNames =
        [
            "Housing", "Groceries", "Restaurants", "Transportation", "Health", "Education",
            "Leisure", "Subscriptions", "Shopping", "Salary", "Investments", "Other"
        ];

        /// <summary>Any language other than <see cref="Language.EN"/> gets the Portuguese names.</summary>
        public static IReadOnlyList<string> Names(Language language)
            => language == Language.EN ? EnglishNames : PortugueseNames;

        /// <summary>The names are fixed and covered by tests, so creating them never fails.</summary>
        public static IReadOnlyList<Category> CreateFor(UserId userId, Language language)
            => Names(language)
                .Select(name => Category.Create(userId.Value, name).Value!)
                .ToList();
    }
}
