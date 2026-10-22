using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;

namespace LifeManager.Application.StatementImports.DTOs
{
    /// <param name="OriginalDescription">What the bank wrote.</param>
    /// <param name="DuplicateOfMonthlySummaryId">With <paramref name="DuplicateOfTransactionId"/>: the user's transaction this line seems to repeat.</param>
    /// <param name="SuggestionUses">How many earlier decisions back the suggestion.</param>
    /// <param name="EditedByUser">The user changed this row directly; a suggestion no longer applies to it.</param>
    /// <param name="Date">The bank's date.</param>
    /// <param name="EffectiveDate">The date the row is posted on: <paramref name="Date"/>, or the first day of the next month when shifted.</param>
    /// <param name="ShiftLearned">The shift came from what the user decided for this merchant before.</param>
    public record StatementImportRowResponseDto(
        int Id,
        DateOnly Date,
        string Description,
        string OriginalDescription,
        decimal Amount,
        MoneyFlowType Type,
        int? CategoryId,
        string? CategoryName,
        bool Included,
        int? DuplicateOfTransactionId,
        int? DuplicateOfMonthlySummaryId,
        SuggestionSource SuggestionSource,
        SuggestionConfidence? SuggestionConfidence,
        int SuggestionUses,
        bool EditedByUser,
        DateOnly EffectiveDate,
        bool ShiftToNextMonth,
        bool ShiftLearned);

    /// <param name="SimilarCount">Other rows of the same merchant, not edited directly, filed differently: what "apply to similar" would change.</param>
    public record StatementImportRowUpdateResultDto(StatementImportRowResponseDto Row, int SimilarCount);

    public record StatementImportRowsChangedDto(int UpdatedCount);
}
