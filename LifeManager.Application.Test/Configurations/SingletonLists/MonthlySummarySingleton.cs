using LifeManager.Domain.MonthlySummaries;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class MonthlySummarySingleton : List<MonthlySummary>
    {
        private MonthlySummarySingleton() { }

        private static readonly Lazy<MonthlySummarySingleton> lazy = new(() => new MonthlySummarySingleton());

        public static MonthlySummarySingleton Instance => lazy.Value;
    }
}
