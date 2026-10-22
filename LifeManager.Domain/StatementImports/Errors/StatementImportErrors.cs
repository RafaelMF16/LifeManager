using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.StatementImports.Errors
{
    public static class StatementImportErrors
    {
        public static readonly Error NotFound = Error.NotFound("StatementImport.NotFound", "Statement import not found");
        public static readonly Error RowNotFound = Error.NotFound("StatementImport.RowNotFound", "Statement import row not found");

        public static readonly Error FileNameIsNullOrWhiteSpace = Error.Validation("StatementImport.FileNameIsNullOrWhiteSpace", "The file name is required");
        public static readonly Error FileNameTooLong = Error.Validation("StatementImport.FileNameTooLong", "The file name cannot be longer than 255 characters");
        public static readonly Error AccountKeyTooLong = Error.Validation("StatementImport.AccountKeyTooLong", "The account key cannot be longer than 100 characters");
        public static readonly Error InvalidSource = Error.Validation("StatementImport.InvalidSource", "The source must be Ofx or Csv");
        public static readonly Error Empty = Error.Validation("StatementImport.Empty", "The statement has no lines to import");
        public static readonly Error TooManyLines = Error.Validation("StatementImport.TooManyLines", "A statement can have at most 5000 lines; split the file");
        public static readonly Error AmountZero = Error.Validation("StatementImport.AmountZero", "A statement line cannot have a zero amount");
        public static readonly Error DateOutOfRange = Error.Validation("StatementImport.DateOutOfRange", "A statement line date must be between 2000-01-01 and today");
        public static readonly Error InvalidMonth = Error.Validation("StatementImport.InvalidMonth", "Month must be in the yyyy-MM format");
        public static readonly Error TooManyRowsSelected = Error.Validation("StatementImport.TooManyRowsSelected", "At most 100 rows can be changed at once");
        public static readonly Error NothingToChange = Error.Validation("StatementImport.NothingToChange", "Choose a category, a type or whether the rows are included");
        public static readonly Error NothingToImport = Error.Validation("StatementImport.NothingToImport", "Include at least one line before confirming the import");
        public static readonly Error UncategorizedRows = Error.Validation("StatementImport.UncategorizedRows", "Every included line needs a category before confirming the import");

        public static readonly Error AlreadyCommitted = Error.Conflict("StatementImport.AlreadyCommitted", "This import was already confirmed");
    }
}
