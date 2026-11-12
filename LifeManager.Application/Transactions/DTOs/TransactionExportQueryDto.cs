using LifeManager.Application.Transactions.Export;
using LifeManager.Domain.Transactions.Enums;

namespace LifeManager.Application.Transactions.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Transactions/Export</c>.</summary>
    public record TransactionExportQueryDto
    {
        /// <summary>First month, "yyyy-MM".</summary>
        public string? From { get; init; }

        /// <summary>Last month, "yyyy-MM".</summary>
        public string? To { get; init; }

        public TransactionTypeFilter Type { get; init; } = TransactionTypeFilter.All;
        public TransactionCsvFormat Format { get; init; } = TransactionCsvFormat.PtBr;
    }
}
