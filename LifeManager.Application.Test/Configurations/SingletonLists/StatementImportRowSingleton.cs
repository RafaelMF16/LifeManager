using LifeManager.Domain.StatementImports;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class StatementImportRowSingleton : List<StatementImportRow>
    {
        private StatementImportRowSingleton() { }

        private static readonly Lazy<StatementImportRowSingleton> lazy = new(() => new StatementImportRowSingleton());

        public static StatementImportRowSingleton Instance => lazy.Value;
    }
}
