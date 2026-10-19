using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.ValueObjects;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Domain.StatementImports
{
    /// <summary>
    /// A bank statement the user uploaded, kept as a draft (one <see cref="StatementImportRow"/> per line) while they
    /// review it, possibly over several visits and devices. Confirming it posts the included lines as transactions,
    /// opening any month the user doesn't have yet, of any year; then the rows are dropped and only this record stays,
    /// as history.
    /// </summary>
    public class StatementImport
    {
        public const int FileNameMaxLength = 255;
        public const int MaxLines = 5000;

        /// <summary>A draft left this long without being confirmed is deleted by the cleanup job.</summary>
        public const int DraftRetentionDays = 30;

        public StatementImportId? Id { get; private set; }
        public UserId UserId { get; }
        public string FileName { get; }
        public StatementImportSource Source { get; }
        public StatementImportStatus Status { get; private set; }

        /// <summary>Lines kept from the file (repeated ones are dropped when the draft is created).</summary>
        public int RowCount { get; }

        /// <summary>The period the file covers, from its first to its last line.</summary>
        public DateOnly FirstDate { get; }
        public DateOnly LastDate { get; }

        public DateTimeOffset CreatedAt { get; }
        public DateTimeOffset? CommittedAt { get; private set; }

        /// <summary>Transactions posted by the confirmation (null while a draft).</summary>
        public int? ImportedCount { get; private set; }

        /// <summary>Months the confirmation had to open (null while a draft).</summary>
        public int? CreatedMonthCount { get; private set; }

        private StatementImport(
            UserId userId,
            string fileName,
            StatementImportSource source,
            StatementImportStatus status,
            int rowCount,
            DateOnly firstDate,
            DateOnly lastDate,
            DateTimeOffset createdAt)
        {
            UserId = userId;
            FileName = fileName;
            Source = source;
            Status = status;
            RowCount = rowCount;
            FirstDate = firstDate;
            LastDate = lastDate;
            CreatedAt = createdAt;
        }

        /// <param name="rows">The draft's rows, already validated one by one (<see cref="StatementImportRow.Create"/>).</param>
        public static Result<StatementImport> Create(int idUser, string? fileName, StatementImportSource source, IReadOnlyCollection<StatementImportRow> rows, DateTimeOffset now)
        {
            var nameResult = ValidateFileName(fileName);
            if (!nameResult.IsSuccess)
                return nameResult.Error;

            if (!Enum.IsDefined(source))
                return StatementImportErrors.InvalidSource;

            var lineCountResult = ValidateLineCount(rows.Count);
            if (!lineCountResult.IsSuccess)
                return lineCountResult.Error;

            return new StatementImport(
                new UserId(idUser),
                nameResult.Value,
                source,
                StatementImportStatus.Draft,
                rows.Count,
                rows.Min(row => row.Date),
                rows.Max(row => row.Date),
                now);
        }

        /// <summary>Checked before reading every line, so a huge file is refused up front.</summary>
        public static Result ValidateLineCount(int lineCount)
        {
            if (lineCount == 0)
                return StatementImportErrors.Empty;

            if (lineCount > MaxLines)
                return StatementImportErrors.TooManyLines;

            return Result.Success();
        }

        /// <summary>Rehydrates a stored import, so tests and the repository mock can seed and copy any state.</summary>
        internal static StatementImport FromPersistence(
            int id,
            int idUser,
            string fileName,
            StatementImportSource source,
            StatementImportStatus status,
            int rowCount,
            DateOnly firstDate,
            DateOnly lastDate,
            DateTimeOffset createdAt,
            DateTimeOffset? committedAt = null,
            int? importedCount = null,
            int? createdMonthCount = null)
        {
            var statementImport = new StatementImport(new UserId(idUser), fileName, source, status, rowCount, firstDate, lastDate, createdAt)
            {
                CommittedAt = committedAt,
                ImportedCount = importedCount,
                CreatedMonthCount = createdMonthCount
            };
            statementImport.AssignId(id);

            return statementImport;
        }

        public bool IsDraft => Status == StatementImportStatus.Draft;

        /// <summary>A confirmed import is history: its rows are gone and nothing in it can change.</summary>
        public Result EnsureDraft()
            => IsDraft ? Result.Success() : StatementImportErrors.AlreadyCommitted;

        public void MarkCommitted(DateTimeOffset now, int importedCount, int createdMonthCount)
        {
            Status = StatementImportStatus.Committed;
            CommittedAt = now;
            ImportedCount = importedCount;
            CreatedMonthCount = createdMonthCount;
        }

        public void AssignId(int id)
        {
            Id = new StatementImportId(id);
        }

        private static Result<string> ValidateFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return StatementImportErrors.FileNameIsNullOrWhiteSpace;

            var trimmed = fileName.Trim();
            if (trimmed.Length > FileNameMaxLength)
                return StatementImportErrors.FileNameTooLong;

            return trimmed;
        }

        public override bool Equals(object? obj)
        {
            if (obj is not StatementImport other)
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
