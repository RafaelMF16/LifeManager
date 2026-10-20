using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.ValueObjects;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// One line of a <see cref="StatementImport"/> draft: what the bank said, and how the user wants it posted. It has
    /// the shape of a transaction (type, category, amount, description, date) but lives in no month until the import is
    /// confirmed, so its date can still move to any month.
    /// </summary>
    public class StatementImportRow
    {
        public const int OriginalDescriptionMaxLength = 255;

        /// <summary>The last days of a month in which a learned "next month" decision files a row on its own (<see cref="IsInShiftWindow"/>).</summary>
        public const int ShiftWindowDays = 5;

        public StatementImportRowId? Id { get; private set; }
        public StatementImportId StatementImportId { get; private set; }
        public DateOnly Date { get; private set; }

        /// <summary>The line as the bank wrote it (spaces collapsed), shown next to the editable description.</summary>
        public string OriginalDescription { get; }

        /// <summary>See <see cref="Categorization.MerchantKey"/>; taken from <see cref="OriginalDescription"/>, so it never changes.</summary>
        public string MerchantKey { get; }

        public TransactionDescription Description { get; private set; }

        /// <summary>Persisted copy of <see cref="TransactionDescription.NormalizedValue"/>, for searching.</summary>
        public string NormalizedDescription { get; private set; }

        public TransactionAmount Amount { get; private set; }
        public MoneyFlowType Type { get; private set; }

        /// <summary>Persisted <see cref="Amount"/> with the sign of <see cref="Type"/>, as on transactions (sorting, totals).</summary>
        public decimal SignedAmount { get; private set; }

        public CategoryId? CategoryId { get; private set; }

        /// <summary>Whether the confirmation posts it.</summary>
        public bool Included { get; private set; }

        /// <summary>See <see cref="ImportExternalId"/>.</summary>
        public string? ExternalId { get; }

        /// <summary>The user's transaction this line seems to repeat (a link for the review screen; it isn't a foreign key).</summary>
        public int? DuplicateOfTransactionId { get; private set; }
        public int? DuplicateOfMonthlySummaryId { get; private set; }

        public SuggestionSource SuggestionSource { get; private set; }
        public SuggestionConfidence? SuggestionConfidence { get; private set; }
        public int SuggestionUses { get; private set; }

        /// <summary>
        /// Set once the user edits this row directly (alone or among selected rows). Spreading a decision to similar
        /// rows (<see cref="CopyDecisionFrom"/>) leaves such rows alone.
        /// </summary>
        public bool EditedByUser { get; private set; }

        /// <summary>
        /// The row counts in the month after its date: a salary paid on the last business day of August is September's.
        /// It is posted on <see cref="EffectiveDate"/>, while <see cref="Date"/> keeps the bank's date.
        /// </summary>
        public bool ShiftToNextMonth { get; private set; }

        /// <summary>The shift came from what the user decided for this merchant before, not from the user on this row.</summary>
        public bool ShiftLearned { get; private set; }

        /// <summary>
        /// The date the row is posted on: <see cref="Date"/>, or the first day of the next month when shifted. Persisted
        /// so the draft's months are grouped and filtered by it in SQL.
        /// </summary>
        public DateOnly EffectiveDate { get; private set; }

        public bool IsDuplicate => DuplicateOfTransactionId is not null;
        public YearMonth Month => YearMonth.From(EffectiveDate);

        private StatementImportRow(
            DateOnly date,
            string originalDescription,
            string merchantKey,
            TransactionDescription description,
            TransactionAmount amount,
            MoneyFlowType type,
            string? externalId)
        {
            StatementImportId = null!;
            Date = date;
            EffectiveDate = date;
            OriginalDescription = originalDescription;
            MerchantKey = merchantKey;
            Description = description;
            NormalizedDescription = description.NormalizedValue;
            Amount = amount;
            Type = type;
            SignedAmount = ToSignedAmount(type, amount);
            ExternalId = externalId;
            Included = true;
            SuggestionSource = SuggestionSource.None;
        }

        /// <summary>
        /// Reads a bank line: money going out becomes an expense and money coming in an income (a suggestion may then
        /// turn it into an investment), and the description is cut to what a transaction holds.
        /// </summary>
        public static Result<StatementImportRow> Create(StatementLine line, string? accountKey, DateOnly today)
        {
            if (line.Amount == 0)
                return StatementImportErrors.AmountZero;

            var amountResult = TransactionAmount.Create(Math.Abs(line.Amount));
            if (!amountResult.IsSuccess)
                return amountResult.Error;

            var dateResult = ValidateDate(line.Date, today);
            if (!dateResult.IsSuccess)
                return dateResult.Error;

            var original = CollapseSpaces(line.Description);
            if (original.Length == 0)
                return TransactionErrors.DescriptionIsNullOrWhiteSpace;

            original = Truncate(original, OriginalDescriptionMaxLength);

            var descriptionResult = TransactionDescription.Create(Truncate(original, TransactionDescription.MaxLength));
            if (!descriptionResult.IsSuccess)
                return descriptionResult.Error;

            var type = line.Amount > 0 ? MoneyFlowType.Income : MoneyFlowType.Expense;

            return new StatementImportRow(
                line.Date,
                original,
                Categorization.MerchantKey.From(original),
                descriptionResult.Value,
                amountResult.Value,
                type,
                ImportExternalId.From(accountKey, line.Fitid));
        }

        /// <summary>Rehydrates a stored row, so tests and the repository mock can seed and copy any state.</summary>
        internal static StatementImportRow FromPersistence(
            int id,
            int idStatementImport,
            DateOnly date,
            string originalDescription,
            string merchantKey,
            string description,
            decimal amount,
            MoneyFlowType type,
            int? idCategory,
            bool included,
            string? externalId = null,
            int? duplicateOfTransactionId = null,
            int? duplicateOfMonthlySummaryId = null,
            SuggestionSource suggestionSource = SuggestionSource.None,
            SuggestionConfidence? suggestionConfidence = null,
            int suggestionUses = 0,
            bool editedByUser = false,
            bool shiftToNextMonth = false,
            bool shiftLearned = false)
        {
            var row = new StatementImportRow(
                date,
                originalDescription,
                merchantKey,
                TransactionDescription.FromPersistence(description),
                TransactionAmount.FromPersistence(amount),
                type,
                externalId)
            {
                StatementImportId = new StatementImportId(idStatementImport),
                CategoryId = idCategory is null ? null : new CategoryId(idCategory.Value),
                Included = included,
                DuplicateOfTransactionId = duplicateOfTransactionId,
                DuplicateOfMonthlySummaryId = duplicateOfMonthlySummaryId,
                SuggestionSource = suggestionSource,
                SuggestionConfidence = suggestionConfidence,
                SuggestionUses = suggestionUses,
                EditedByUser = editedByUser,
                ShiftLearned = shiftLearned
            };
            row.SetShift(shiftToNextMonth);
            row.AssignId(id);

            return row;
        }

        /// <summary>Files the row the way the user filed this merchant before; "ignore" leaves it out of the import.</summary>
        public void ApplySuggestion(CategorySuggestion suggestion)
        {
            SetType(suggestion.Type);
            SuggestionSource = suggestion.Source;
            SuggestionConfidence = suggestion.Confidence;
            SuggestionUses = suggestion.Uses;

            if (suggestion.Action == RuleAction.Ignore)
                Included = false;
            else
                CategoryId = suggestion.CategoryId;
        }

        /// <summary>
        /// Files a line the bank wrote as an investment movement (<see cref="InvestmentDetector"/>): an application
        /// becomes an investment, in <paramref name="categoryId"/> when one is known, and a redemption is left out, since
        /// it is neither income nor new money invested.
        /// </summary>
        public void ApplyInvestmentDetection(InvestmentMovement movement, CategoryId? categoryId)
        {
            if (movement == InvestmentMovement.Application)
            {
                SetType(MoneyFlowType.Investment);
                CategoryId = categoryId;
                SuggestionSource = SuggestionSource.InvestmentApplication;
            }
            else
            {
                Included = false;
                SuggestionSource = SuggestionSource.InvestmentRedemption;
            }

            SuggestionConfidence = Categorization.Enums.SuggestionConfidence.High;
            SuggestionUses = 0;
        }

        /// <summary>
        /// A transfer between the user's own accounts (<see cref="OwnTransferDetector"/>) is left out: the money only
        /// moves, so it is neither income nor spending.
        /// </summary>
        public void ApplyOwnTransferDetection()
        {
            Included = false;
            SuggestionSource = SuggestionSource.OwnTransfer;
            SuggestionConfidence = Categorization.Enums.SuggestionConfidence.High;
            SuggestionUses = 0;
        }

        /// <summary>The line is already among the user's transactions (or likely so): it is left out unless the user includes it.</summary>
        public void MarkDuplicateOf(int transactionId, int monthlySummaryId)
        {
            DuplicateOfTransactionId = transactionId;
            DuplicateOfMonthlySummaryId = monthlySummaryId;
            Included = false;
        }

        /// <summary>The user's edit of the row: the same rules as a transaction, plus a date between 2000 and today.</summary>
        public Result<StatementImportRow> Edit(
            MoneyFlowType type,
            int? idCategory,
            decimal amount,
            string description,
            DateOnly date,
            bool included,
            DateOnly today,
            bool shiftToNextMonth = false)
        {
            if (!Enum.IsDefined(type))
                return TransactionErrors.InvalidType;

            var descriptionResult = TransactionDescription.Create(description);
            if (!descriptionResult.IsSuccess)
                return descriptionResult.Error;

            var amountResult = TransactionAmount.Create(amount);
            if (!amountResult.IsSuccess)
                return amountResult.Error;

            var dateResult = ValidateDate(date, today);
            if (!dateResult.IsSuccess)
                return dateResult.Error;

            Description = descriptionResult.Value;
            NormalizedDescription = descriptionResult.Value.NormalizedValue;
            Amount = amountResult.Value;
            SetType(type);
            CategoryId = idCategory is null ? null : new CategoryId(idCategory.Value);
            Date = date;
            Included = included;
            SetShift(shiftToNextMonth);
            ShiftLearned = false;
            EditedByUser = true;

            return this;
        }

        /// <summary>Changes selected rows at once; only the given values change.</summary>
        public Result<StatementImportRow> EditInBulk(MoneyFlowType? type, int? idCategory, bool? included, bool? shiftToNextMonth = null)
        {
            if (type is not null && !Enum.IsDefined(type.Value))
                return TransactionErrors.InvalidType;

            if (type is not null)
                SetType(type.Value);

            if (idCategory is not null)
                CategoryId = new CategoryId(idCategory.Value);

            if (included is not null)
                Included = included.Value;

            if (shiftToNextMonth is not null)
                SetShift(shiftToNextMonth.Value);

            ShiftLearned = false;
            EditedByUser = true;
            return this;
        }

        /// <summary>
        /// Files this row like <paramref name="source"/>, a row of the same merchant the user just decided on. A duplicate
        /// stays out even when the source is included. It now follows the user's decision rather than a suggestion.
        /// </summary>
        public void CopyDecisionFrom(StatementImportRow source)
        {
            SetType(source.Type);
            CategoryId = source.CategoryId;
            Included = source.Included && !IsDuplicate;
            SetShift(source.ShiftToNextMonth);
            ShiftLearned = false;
            SuggestionSource = SuggestionSource.None;
            SuggestionConfidence = null;
            SuggestionUses = 0;
        }

        /// <summary>The user counted this merchant's lines in the next month before, and this one is at the end of its month.</summary>
        public void ApplyLearnedShift()
        {
            SetShift(true);
            ShiftLearned = true;
        }

        /// <summary>Whether <paramref name="date"/> is among the last <see cref="ShiftWindowDays"/> days of its month.</summary>
        public static bool IsInShiftWindow(DateOnly date)
            => date.Day > DateTime.DaysInMonth(date.Year, date.Month) - ShiftWindowDays;

        public void AssignImport(StatementImportId statementImportId)
        {
            StatementImportId = statementImportId;
        }

        public void AssignId(int id)
        {
            Id = new StatementImportRowId(id);
        }

        private void SetShift(bool shiftToNextMonth)
        {
            ShiftToNextMonth = shiftToNextMonth;
            EffectiveDate = shiftToNextMonth ? YearMonth.From(Date).AddMonths(1).FirstDay : Date;
        }

        private void SetType(MoneyFlowType type)
        {
            Type = type;
            SignedAmount = ToSignedAmount(type, Amount);
        }

        private static Result ValidateDate(DateOnly date, DateOnly today)
        {
            if (date.Year < YearMonth.MinYear || date > today)
                return StatementImportErrors.DateOutOfRange;

            return Result.Success();
        }

        private static string CollapseSpaces(string? value)
            => string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        private static string Truncate(string value, int maxLength)
            => value.Length <= maxLength ? value : value[..maxLength].TrimEnd();

        private static decimal ToSignedAmount(MoneyFlowType type, TransactionAmount amount)
            => type == MoneyFlowType.Income ? amount.Value : -amount.Value;

        public override bool Equals(object? obj)
        {
            if (obj is not StatementImportRow other)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (Id is null || other.Id is null)
                return false;

            return Id.Equals(other.Id);
        }

        public override int GetHashCode()
            => Id?.GetHashCode() ?? base.GetHashCode();
    }
}
