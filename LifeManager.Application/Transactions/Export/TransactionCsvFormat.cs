namespace LifeManager.Application.Transactions.Export
{
    /// <summary>The spreadsheet conventions of the CSV export.</summary>
    public enum TransactionCsvFormat
    {
        /// <summary>Brazilian Excel: <c>;</c> separator, decimal comma, <c>dd/MM/yyyy</c>, headers in Portuguese.</summary>
        PtBr = 1,

        /// <summary><c>,</c> separator, decimal point, <c>yyyy-MM-dd</c>, headers in English.</summary>
        EnUs = 2
    }
}
