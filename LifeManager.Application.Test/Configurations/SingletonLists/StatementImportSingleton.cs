using LifeManager.Domain.StatementImports;

namespace LifeManager.Application.Test.Configurations.SingletonLists
{
    public sealed class StatementImportSingleton : List<StatementImport>
    {
        private StatementImportSingleton() { }

        private static readonly Lazy<StatementImportSingleton> lazy = new(() => new StatementImportSingleton());

        public static StatementImportSingleton Instance => lazy.Value;
    }
}
