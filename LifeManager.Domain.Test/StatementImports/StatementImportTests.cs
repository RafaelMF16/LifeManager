using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Errors;

namespace LifeManager.Domain.Test.StatementImports
{
    public class StatementImportTests
    {
        private static readonly DateOnly Today = new(2026, 10, 19);
        private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

        private static StatementImportRow Row(DateOnly date)
            => StatementImportRow.Create(new StatementLine(date, "Mercado", -10m, null), null, Today).Value!;

        [Fact]
        public void Create_ShouldOpenADraftCoveringTheLinesPeriod()
        {
            var result = StatementImport.Create(1, "  extrato.ofx  ", StatementImportSource.Ofx, [Row(new DateOnly(2024, 3, 9)), Row(new DateOnly(2023, 11, 2))], Now);

            Assert.True(result.IsSuccess);
            var statementImport = result.Value;
            Assert.Equal("extrato.ofx", statementImport.FileName);
            Assert.Equal(StatementImportStatus.Draft, statementImport.Status);
            Assert.True(statementImport.IsDraft);
            Assert.Equal(2, statementImport.RowCount);
            Assert.Equal(new DateOnly(2023, 11, 2), statementImport.FirstDate);
            Assert.Equal(new DateOnly(2024, 3, 9), statementImport.LastDate);
            Assert.Equal(Now, statementImport.CreatedAt);
            Assert.Null(statementImport.ImportedCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("  ")]
        public void Create_ShouldFail_WhenTheFileNameIsBlank(string? fileName)
        {
            var result = StatementImport.Create(1, fileName, StatementImportSource.Csv, [Row(Today)], Now);

            Assert.Equal(StatementImportErrors.FileNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public void Create_ShouldFail_WhenTheFileNameIsTooLong()
        {
            var result = StatementImport.Create(1, new string('a', 256), StatementImportSource.Csv, [Row(Today)], Now);

            Assert.Equal(StatementImportErrors.FileNameTooLong, result.Error);
        }

        [Fact]
        public void Create_ShouldFail_WhenTheSourceIsNotDefined()
        {
            var result = StatementImport.Create(1, "a.csv", (StatementImportSource)9, [Row(Today)], Now);

            Assert.Equal(StatementImportErrors.InvalidSource, result.Error);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(StatementImport.MaxLines, true)]
        [InlineData(StatementImport.MaxLines + 1, false)]
        public void ValidateLineCount_ShouldAcceptOneToMaxLines(int lineCount, bool valid)
        {
            Assert.Equal(valid, StatementImport.ValidateLineCount(lineCount).IsSuccess);
        }

        [Fact]
        public void EnsureDraft_ShouldFail_OnceCommitted()
        {
            var statementImport = StatementImport.Create(1, "a.ofx", StatementImportSource.Ofx, [Row(Today)], Now).Value!;
            Assert.True(statementImport.EnsureDraft().IsSuccess);

            statementImport.MarkCommitted(Now, importedCount: 1, createdMonthCount: 0);

            Assert.Equal(StatementImportErrors.AlreadyCommitted, statementImport.EnsureDraft().Error);
            Assert.Equal(StatementImportStatus.Committed, statementImport.Status);
            Assert.Equal(1, statementImport.ImportedCount);
            Assert.Equal(0, statementImport.CreatedMonthCount);
            Assert.Equal(Now, statementImport.CommittedAt);
        }

        [Fact]
        public void ImportExternalId_ShouldDependOnTheAccount()
        {
            Assert.NotEqual(ImportExternalId.From("account-a", "1"), ImportExternalId.From("account-b", "1"));
            Assert.Equal(ImportExternalId.Length, ImportExternalId.From("account-a", "1")!.Length);
            Assert.Null(ImportExternalId.From(null, "1"));
            Assert.Null(ImportExternalId.From("account-a", " "));
        }
    }
}
