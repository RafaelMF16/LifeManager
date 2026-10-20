using LifeManager.Domain.Categorization;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class CategorizationRuleSingleton : List<CategorizationRule>
    {
        private CategorizationRuleSingleton() { }

        private static readonly Lazy<CategorizationRuleSingleton> lazy = new(() => new CategorizationRuleSingleton());

        public static CategorizationRuleSingleton Instance => lazy.Value;
    }
}
