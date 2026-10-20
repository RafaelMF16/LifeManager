using LifeManager.Domain.StatementImports.Enums;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <summary>Body of <c>POST /api/StatementImports</c>: the lines the frontend read from the bank file.</summary>
    /// <param name="AccountKey">An id of the account the file belongs to (OFX), so its lines are recognized when imported again.</param>
    public record StatementImportDto(string? FileName, StatementImportSource Source, string? AccountKey, IReadOnlyList<StatementLineDto>? Lines);

    /// <param name="Amount">Negative when money left the account.</param>
    /// <param name="Fitid">The bank's id of the line (OFX <c>FITID</c>), when there is one.</param>
    public record StatementLineDto(DateOnly Date, string? Description, decimal Amount, string? Fitid);
}
