namespace LifeManager.Domain.StatementImports.Enums
{
    public enum StatementImportStatus
    {
        /// <summary>Being reviewed: nothing was posted yet.</summary>
        Draft = 1,

        /// <summary>Confirmed: its lines became transactions and its rows were dropped.</summary>
        Committed = 2
    }
}
