using LifeManager.Application.StatementImports.DTOs;
using LifeManager.Application.StatementImports.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.StatementImports.Mocks;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.StatementImports.Interfaces;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.StatementImports
{
    [Collection("ApplicationServices")]
    public class StatementImportServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 19);
        private const string Account = "bank-0001-12345";

        // Categories 1-4 belong to the first user; 5 to the second.
        private const int Restaurants = 1;
        private const int Market = 2;
        private const int Salary = 3;
        private const int Savings = 4;
        private const int OtherUserCategory = 5;

        // The first user already has March 2024 (id 1); the second user has their own March 2024 (id 2).
        private const int March2024Id = 1;
        private const int OtherUserMarch2024Id = 2;

        private readonly StatementImportService _service;
        private readonly StatementImportRepositoryMock _repository;

        public StatementImportServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<StatementImportService>();
            _repository = (StatementImportRepositoryMock)ServiceProvider.GetRequiredService<IStatementImportRepository>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            StatementImportSingleton.Instance.Clear();
            StatementImportRowSingleton.Instance.Clear();
            CategorizationRuleSingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();
            UserSingleton.Instance.Clear();

            SeedCategory(Restaurants, FirstUserId, "Restaurantes");
            SeedCategory(Market, FirstUserId, "Mercado");
            SeedCategory(Salary, FirstUserId, "Salário");
            SeedCategory(Savings, FirstUserId, "Reserva");
            SeedCategory(OtherUserCategory, SecondUserId, "Outros");

            MonthlySummarySingleton.Instance.AddRange(
                MonthlySummary.FromPersistence(March2024Id, FirstUserId.Value, 3, 2024, 0, 0, 0),
                MonthlySummary.FromPersistence(OtherUserMarch2024Id, SecondUserId.Value, 3, 2024, 0, 0, 0));
        }

        // ---------- Create ----------

        [Fact]
        public async Task CreateAsync_ShouldStoreADraftWithOneRowPerLine()
        {
            var result = await CreateAsync(
                Line(2024, 3, 5, -42.90m, "COMPRA CARTAO IFOOD *RESTAURANTE", "F1"),
                Line(2023, 12, 20, 5000m, "SALARIO EMPRESA XPTO", "F2"));

            Assert.True(result.IsSuccess);
            var draft = result.Value;
            Assert.Equal("extrato.ofx", draft.FileName);
            Assert.Equal(StatementImportStatus.Draft, draft.Status);
            Assert.Equal(2, draft.RowCount);
            Assert.Equal(new DateOnly(2023, 12, 20), draft.FirstDate);
            Assert.Equal(new DateOnly(2024, 3, 5), draft.LastDate);

            var rows = StatementImportRowSingleton.Instance;
            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(draft.Id, row.StatementImportId.Value));
            Assert.Contains(rows, row => row.Type == MoneyFlowType.Expense && row.Amount.Value == 42.90m && row.MerchantKey == "ifood restaurante");
            Assert.Contains(rows, row => row.Type == MoneyFlowType.Income && row.Amount.Value == 5000m);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldKeepARepeatedBankLineOnce()
        {
            var result = await CreateAsync(
                Line(2024, 3, 5, -10m, "NETFLIX", "F1"),
                Line(2024, 3, 5, -10m, "NETFLIX", "F1"),
                Line(2024, 3, 5, -10m, "NETFLIX", "F2"));

            Assert.Equal(2, result.Value!.RowCount);
        }

        [Fact]
        public async Task CreateAsync_ShouldFail_WhenThereAreNoLines()
        {
            var result = await _service.CreateAsync(new StatementImportDto("a.ofx", StatementImportSource.Ofx, Account, []), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.Empty, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldFail_WhenThereAreTooManyLines()
        {
            var lines = Enumerable.Range(0, StatementImport.MaxLines + 1).Select(index => Line(2024, 3, 5, -1m, $"Linha {index}")).ToArray();

            var result = await CreateAsync(lines);

            Assert.Equal(StatementImportErrors.TooManyLines, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldFail_WhenTheAccountKeyIsTooLong()
        {
            var result = await _service.CreateAsync(
                new StatementImportDto("a.ofx", StatementImportSource.Ofx, new string('a', 101), [Line(2024, 3, 5, -1m, "X")]), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.AccountKeyTooLong, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldFail_WhenALineIsInvalid()
        {
            var result = await CreateAsync(Line(2024, 3, 5, -1m, "Ok"), Line(2026, 10, 20, -1m, "Amanhã"));

            Assert.Equal(StatementImportErrors.DateOutOfRange, result.Error);
            Assert.Empty(StatementImportSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldFail_WhenTheFileNameIsBlank()
        {
            var result = await _service.CreateAsync(new StatementImportDto(" ", StatementImportSource.Csv, null, [Line(2024, 3, 5, -1m, "X")]), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.FileNameIsNullOrWhiteSpace, result.Error);
        }

        [Fact]
        public async Task CreateAsync_ShouldSuggestCategoriesFromLearnedRules()
        {
            SeedRule("ifood restaurante", Restaurants, hits: 5);
            SeedRule("aplicacao cdb", Savings, hits: 2, type: MoneyFlowType.Investment);

            await CreateAsync(
                Line(2024, 3, 5, -42.90m, "COMPRA CARTAO IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -1000m, "APLICACAO CDB 123"));

            var ifood = SingleRow("ifood restaurante");
            Assert.Equal(new CategoryId(Restaurants), ifood.CategoryId);
            Assert.Equal(SuggestionSource.Rule, ifood.SuggestionSource);
            Assert.Equal(SuggestionConfidence.High, ifood.SuggestionConfidence);
            Assert.Equal(5, ifood.SuggestionUses);

            var cdb = SingleRow("aplicacao cdb");
            Assert.Equal(MoneyFlowType.Investment, cdb.Type);
            Assert.Equal(new CategoryId(Savings), cdb.CategoryId);
        }

        [Fact]
        public async Task CreateAsync_ShouldSuggestCategoriesFromTheUsersTransactions()
        {
            SeedTransaction(March2024Id, MoneyFlowType.Expense, Market, 100m, "Mercado", new DateOnly(2024, 3, 1));
            SeedTransaction(March2024Id, MoneyFlowType.Expense, Market, 120m, "Mercado", new DateOnly(2024, 3, 8));

            await CreateAsync(Line(2024, 2, 10, -90m, "COMPRA CARTAO MERCADO 10/02"));

            var row = SingleRow("mercado");
            Assert.Equal(new CategoryId(Market), row.CategoryId);
            Assert.Equal(SuggestionSource.History, row.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldNotLearnFromAnotherUser()
        {
            SeedRule("ifood restaurante", OtherUserCategory, hits: 5, userId: SecondUserId);

            await CreateAsync(Line(2024, 3, 5, -42.90m, "IFOOD *RESTAURANTE"));

            Assert.Null(SingleRow("ifood restaurante").CategoryId);
        }

        [Fact]
        public async Task CreateAsync_ShouldLeaveOutLinesTheUserIgnoredBefore()
        {
            SeedRule("joao da silva", categoryId: null, hits: 3, action: RuleAction.Ignore);

            await CreateAsync(Line(2024, 3, 5, -500m, "PIX ENVIADO JOAO DA SILVA"));

            var row = SingleRow("joao da silva");
            Assert.False(row.Included);
            Assert.Null(row.CategoryId);
        }

        [Fact]
        public async Task CreateAsync_ShouldLeaveOutLinesAlreadyImported()
        {
            var first = await CreateAsync(Line(2024, 3, 5, -42.90m, "IFOOD *RESTAURANTE", "F1"));
            await FileAllRowsAsync(Restaurants);
            await _service.CommitAsync(first.Value!.Id, FirstUserId, CancellationToken.None);

            await CreateAsync(Line(2024, 3, 5, -42.90m, "IFOOD *RESTAURANTE", "F1"), Line(2024, 3, 6, -15m, "PADARIA", "F2"));

            var repeated = SingleRow("ifood restaurante");
            Assert.True(repeated.IsDuplicate);
            Assert.False(repeated.Included);
            Assert.Equal(March2024Id, repeated.DuplicateOfMonthlySummaryId);
            Assert.False(SingleRow("padaria").IsDuplicate);
        }

        [Fact]
        public async Task CreateAsync_ShouldLeaveOutLinesTypedByHand()
        {
            var transaction = SeedTransaction(March2024Id, MoneyFlowType.Expense, Market, 230.45m, "Compras do mês", new DateOnly(2024, 3, 9));

            await CreateAsync(Line(2024, 3, 9, -230.45m, "COMPRA CARTAO SUPERMERCADO EXTRA"));

            var row = SingleRow("supermercado extra");
            Assert.Equal(transaction.Id!.Value, row.DuplicateOfTransactionId);
            Assert.False(row.Included);
        }

        [Fact]
        public async Task CreateAsync_ShouldNotCompareWithAnotherUsersTransactions()
        {
            SeedTransaction(OtherUserMarch2024Id, MoneyFlowType.Expense, OtherUserCategory, 230.45m, "Compras", new DateOnly(2024, 3, 9));

            await CreateAsync(Line(2024, 3, 9, -230.45m, "SUPERMERCADO EXTRA"));

            Assert.False(SingleRow("supermercado extra").IsDuplicate);
        }

        // ---------- Read ----------

        [Fact]
        public async Task GetByIdAsync_ShouldReturnTheDraftsMonthsWithCountsAndTotals()
        {
            SeedRule("ifood restaurante", Restaurants, hits: 2);
            var created = await CreateAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -10m, "PADARIA"),
                Line(2024, 3, 7, 5000m, "SALARIO"),
                Line(2023, 12, 20, -99.9m, "LOJA"));

            var result = await _service.GetByIdAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var details = result.Value;
            Assert.Equal(4, details.IncludedCount);
            Assert.Equal(3, details.UncategorizedCount);
            Assert.Equal(1, details.SuggestedCount);
            Assert.Equal(0, details.DuplicateCount);
            Assert.Equal(
                [
                    new StatementImportMonthDto("2023-12", null, 1, 1, 1, 0, 0, 0m, 99.9m, 0m),
                    new StatementImportMonthDto("2024-03", March2024Id, 3, 3, 2, 1, 0, 5000m, 50m, 0m)
                ],
                details.Months);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_ForAnotherUsersImport()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));

            var result = await _service.GetByIdAsync(created.Value!.Id, SecondUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldListDraftsFirstThenNewestFirst()
        {
            var timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            var older = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            await FileAllRowsAsync(Market);
            await _service.CommitAsync(older.Value!.Id, FirstUserId, CancellationToken.None);
            timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(1);
            var committedLater = await CreateAsync(Line(2024, 3, 6, -10m, "PADARIA"));
            await FileAllRowsAsync(Market);
            await _service.CommitAsync(committedLater.Value!.Id, FirstUserId, CancellationToken.None);
            timeProvider.UtcNow = timeProvider.UtcNow.AddMinutes(1);
            var draft = await CreateAsync(Line(2024, 3, 7, -10m, "FARMACIA"));

            var result = await _service.GetPagedAsync(new StatementImportListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.Equal([draft.Value!.Id, committedLater.Value!.Id, older.Value!.Id], result.Value!.Items.Select(item => item.Id));
            Assert.Equal(1, result.Value.Items[1].ImportedCount);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFail_WhenThePageSizeIsInvalid()
        {
            var result = await _service.GetPagedAsync(new StatementImportListQueryDto { PageSize = 101 }, FirstUserId, CancellationToken.None);

            Assert.Equal(PagingErrors.InvalidPageSize, result.Error);
        }

        [Fact]
        public async Task DeleteAsync_ShouldDiscardTheDraftAndItsRows()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));

            var result = await _service.DeleteAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(StatementImportSingleton.Instance);
            Assert.Empty(StatementImportRowSingleton.Instance);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFound_ForAnotherUsersImport()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));

            var result = await _service.DeleteAsync(created.Value!.Id, SecondUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NotFound, result.Error);
            Assert.Single(StatementImportSingleton.Instance);
        }

        // ---------- Commit ----------

        [Fact]
        public async Task CommitAsync_ShouldPostTheIncludedRowsOpeningMonthsOfPastYears()
        {
            var created = await CreateAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 7, 5000m, "SALARIO"),
                Line(2021, 6, 30, -99.9m, "LOJA"));
            await FileRowAsync("ifood restaurante", Restaurants);
            await FileRowAsync("salario", Salary, MoneyFlowType.Income);
            await FileRowAsync("loja", Market);

            var result = await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var commit = result.Value;
            Assert.Equal(3, commit.ImportedCount);
            Assert.Equal(0, commit.SkippedCount);
            Assert.Equal(1, commit.CreatedMonthCount);
            Assert.Equal(["2021-06", "2024-03"], commit.Months.Select(month => month.Month));
            Assert.True(commit.Months[0].Created);
            Assert.False(commit.Months[1].Created);
            Assert.Equal(March2024Id, commit.Months[1].MonthlySummaryId);

            var march = MonthlySummarySingleton.Instance.Single(month => month.Id!.Value == March2024Id);
            Assert.Equal(5000m, march.TotalIncome.Value);
            Assert.Equal(40m, march.TotalExpense.Value);

            var june2021 = MonthlySummarySingleton.Instance.Single(month => month.Year.Value == 2021);
            Assert.Equal(FirstUserId, june2021.UserId);
            Assert.Equal(99.9m, june2021.TotalExpense.Value);

            Assert.All(TransactionSingleton.Instance, transaction => Assert.Equal(created.Value.Id, transaction.StatementImportId!.Value));
        }

        [Fact]
        public async Task CommitAsync_ShouldMarkTheImportCommittedAndDropItsRows()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"), Line(2024, 3, 6, -10m, "PADARIA"));
            await FileRowAsync("loja", Market);
            await FileRowAsync("padaria", Market, included: false);

            await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            var details = (await _service.GetByIdAsync(created.Value.Id, FirstUserId, CancellationToken.None)).Value!;
            Assert.Equal(StatementImportStatus.Committed, details.Status);
            Assert.Equal(1, details.ImportedCount);
            Assert.Equal(0, details.CreatedMonthCount);
            Assert.NotNull(details.CommittedAt);
            Assert.Empty(details.Months);
            Assert.Empty(StatementImportRowSingleton.Instance);
            Assert.Single(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task CommitAsync_ShouldLearnFromWhatTheUserDecided()
        {
            var created = await CreateAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -45m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 7, -500m, "PIX ENVIADO JOAO"));
            await FileAllRowsAsync(Restaurants);
            await FileRowAsync("joao", categoryId: null, included: false);

            await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            var rules = CategorizationRuleSingleton.Instance;
            Assert.Equal(2, rules.Count);
            var ifood = rules.Single(rule => rule.MerchantKey == "ifood restaurante");
            Assert.Equal((RuleAction.Categorize, new CategoryId(Restaurants), 2, Today), (ifood.Action, ifood.CategoryId, ifood.Hits, ifood.LastUsedOn));
            var joao = rules.Single(rule => rule.MerchantKey == "joao");
            Assert.Equal((RuleAction.Ignore, (CategoryId?)null, 1), (joao.Action, joao.CategoryId, joao.Hits));
        }

        [Fact]
        public async Task CommitAsync_ShouldAddToExistingRules_SoTheNextImportIsFiledAlready()
        {
            SeedRule("ifood restaurante", Restaurants, hits: 1);
            var first = await CreateAsync(Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"));
            await _service.CommitAsync(first.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(2, CategorizationRuleSingleton.Instance.Single().Hits);

            await CreateAsync(Line(2024, 4, 5, -40m, "IFOOD *RESTAURANTE"));
            var row = SingleRow("ifood restaurante");
            Assert.Equal(SuggestionConfidence.High, row.SuggestionConfidence);
            Assert.Equal(new CategoryId(Restaurants), row.CategoryId);
        }

        [Fact]
        public async Task CommitAsync_ShouldFail_WhenAnIncludedRowHasNoCategory()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"), Line(2024, 3, 6, -10m, "PADARIA"));
            await FileRowAsync("loja", Market);

            var result = await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.UncategorizedRows, result.Error);
            Assert.Empty(TransactionSingleton.Instance);
            Assert.Equal(0, _repository.CommitCallCount);
        }

        [Fact]
        public async Task CommitAsync_ShouldFail_WhenEveryRowIsLeftOut()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            await FileRowAsync("loja", categoryId: null, included: false);

            var result = await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NothingToImport, result.Error);
        }

        [Fact]
        public async Task CommitAsync_ShouldFail_WhenTheImportWasAlreadyCommitted()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            await FileAllRowsAsync(Market);
            await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            var result = await _service.CommitAsync(created.Value.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.AlreadyCommitted, result.Error);
            Assert.Single(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task CommitAsync_ShouldPostOnce_WhenTwoConfirmationsRace()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            await FileAllRowsAsync(Market);
            var importId = created.Value!.Id;
            _repository.BeforeNextCommit = () =>
            {
                // Another request confirms the same draft between this one's checks and its lock.
                var other = StatementImportSingleton.Instance.Single(statementImport => statementImport.Id!.Value == importId);
                other.MarkCommitted(DateTimeOffset.UtcNow, importedCount: 1, createdMonthCount: 0);
            };

            var result = await _service.CommitAsync(importId, FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.AlreadyCommitted, result.Error);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task CommitAsync_ShouldSkipBankLinesPostedMeanwhile()
        {
            var first = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA", "F1"));
            var second = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA", "F1"), Line(2024, 3, 6, -10m, "PADARIA", "F2"));
            await FileAllRowsAsync(Market);
            await _service.CommitAsync(first.Value!.Id, FirstUserId, CancellationToken.None);

            var result = await _service.CommitAsync(second.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(1, result.Value!.ImportedCount);
            Assert.Equal(1, result.Value.SkippedCount);
            Assert.Equal(2, TransactionSingleton.Instance.Count);
            Assert.Equal(50m, MonthlySummarySingleton.Instance.Single(month => month.Id!.Value == March2024Id).TotalExpense.Value);
        }

        [Fact]
        public async Task CommitAsync_ShouldFail_WhenACategoryIsNotTheUsers()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            ReplaceRow(SingleRow("loja"), row => row.EditInBulk(type: null, idCategory: OtherUserCategory, included: null));

            var result = await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task CommitAsync_ShouldReturnNotFound_ForAnotherUsersImport()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            await FileAllRowsAsync(Market);

            var result = await _service.CommitAsync(created.Value!.Id, SecondUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task DeleteAsync_ShouldKeepTheTransactionsOfACommittedImport()
        {
            var created = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA", "F1"));
            await FileAllRowsAsync(Market);
            await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            await _service.DeleteAsync(created.Value.Id, FirstUserId, CancellationToken.None);

            var transaction = Assert.Single(TransactionSingleton.Instance);
            Assert.Null(transaction.StatementImportId);
            Assert.NotNull(transaction.ExternalId);
        }

        // ---------- Cleanup ----------

        [Fact]
        public async Task DeleteStaleDraftsAsync_ShouldDeleteOnlyDraftsOlderThanTheRetention()
        {
            var timeProvider = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            var stale = await CreateAsync(Line(2024, 3, 5, -40m, "LOJA"));
            var committed = await CreateAsync(Line(2024, 3, 6, -10m, "PADARIA"));
            await FileRowAsync("padaria", Market);
            await _service.CommitAsync(committed.Value!.Id, FirstUserId, CancellationToken.None);
            timeProvider.UtcNow = timeProvider.UtcNow.AddDays(StatementImport.DraftRetentionDays - 1);
            var recent = await CreateAsync(Line(2024, 3, 7, -10m, "FARMACIA"));
            timeProvider.UtcNow = timeProvider.UtcNow.AddDays(2);

            var deleted = await _service.DeleteStaleDraftsAsync(CancellationToken.None);

            Assert.Equal(1, deleted);
            Assert.Equal(
                [committed.Value.Id, recent.Value!.Id],
                StatementImportSingleton.Instance.Select(statementImport => statementImport.Id!.Value).Order());
            Assert.DoesNotContain(StatementImportRowSingleton.Instance, row => row.StatementImportId.Value == stale.Value!.Id);
        }

        // ---------- Helpers ----------

        // ---------- Detections ----------

        [Fact]
        public async Task CreateAsync_ShouldFileInvestmentLines_WhenThereIsNoHistory()
        {
            var created = await CreateAsync(
                Line(2024, 3, 5, -500m, "Aplicação RDB"),
                Line(2024, 3, 6, 610m, "Resgate RDB"),
                Line(2024, 3, 7, -42.90m, "COMPRA CARTAO IFOOD *RESTAURANTE"));

            var application = SingleRow("aplicacao rdb");
            Assert.Equal(MoneyFlowType.Investment, application.Type);
            Assert.True(application.Included);
            Assert.Null(application.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentApplication, application.SuggestionSource);

            var redemption = SingleRow("resgate rdb");
            Assert.Equal(MoneyFlowType.Income, redemption.Type);
            Assert.False(redemption.Included);
            Assert.Equal(SuggestionSource.InvestmentRedemption, redemption.SuggestionSource);

            var purchase = SingleRow("ifood restaurante");
            Assert.Equal(MoneyFlowType.Expense, purchase.Type);
            Assert.True(purchase.Included);
            Assert.Equal(SuggestionSource.None, purchase.SuggestionSource);

            var details = (await _service.GetByIdAsync(created.Value!.Id, FirstUserId, CancellationToken.None)).Value!;
            Assert.Equal(2, details.SuggestedCount);
            Assert.Equal(2, details.IncludedCount);
        }

        [Fact]
        public async Task CreateAsync_ShouldFollowTheUsersRule_OverTheInvestmentDetection()
        {
            SeedRule("aplicacao rdb", Market, hits: 2);

            await CreateAsync(Line(2024, 3, 5, -500m, "Aplicação RDB"));

            var row = SingleRow("aplicacao rdb");
            Assert.Equal(MoneyFlowType.Expense, row.Type);
            Assert.Equal(new CategoryId(Market), row.CategoryId);
            Assert.Equal(SuggestionSource.Rule, row.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldFollowTheUsersHistory_OverTheInvestmentDetection()
        {
            SeedTransaction(March2024Id, MoneyFlowType.Income, Savings, 610m, "Resgate RDB", new DateOnly(2024, 3, 1));

            await CreateAsync(Line(2024, 3, 6, 610m, "Resgate RDB"));

            var row = SingleRow("resgate rdb");
            Assert.True(row.Included);
            Assert.Equal(new CategoryId(Savings), row.CategoryId);
            Assert.Equal(SuggestionSource.History, row.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldBorrowTheCategoryOfASimilarInvestment_WhenAnApplicationIsDetected()
        {
            SeedRule("aplicacao cdb", Savings, hits: 2, type: MoneyFlowType.Investment);

            await CreateAsync(Line(2024, 3, 5, -500m, "Aplicação RDB"));

            var row = SingleRow("aplicacao rdb");
            Assert.Equal(MoneyFlowType.Investment, row.Type);
            Assert.Equal(new CategoryId(Savings), row.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentApplication, row.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldIgnoreASimilarSuggestionOfAnotherType_WhenAnInvestmentIsDetected()
        {
            SeedRule("aplicacao cdb", Market, hits: 2);
            SeedRule("resgate cdb", Salary, hits: 2, type: MoneyFlowType.Income);

            await CreateAsync(
                Line(2024, 3, 5, -500m, "Aplicação RDB"),
                Line(2024, 3, 6, 610m, "Resgate RDB"));

            var application = SingleRow("aplicacao rdb");
            Assert.Equal(MoneyFlowType.Investment, application.Type);
            Assert.Null(application.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentApplication, application.SuggestionSource);

            var redemption = SingleRow("resgate rdb");
            Assert.False(redemption.Included);
            Assert.Null(redemption.CategoryId);
            Assert.Equal(SuggestionSource.InvestmentRedemption, redemption.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldLeaveOutTransfersBetweenTheUsersOwnAccounts()
        {
            SeedUser(FirstUserId, "Fulano Beltrano Ciclano");

            await CreateAsync(
                Line(2024, 3, 5, 1000m, "Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\""),
                Line(2024, 3, 6, -28m, "Transferência enviada pelo Pix - Fulano Beltrano Ciclano - •••.000.000-•• - BANCO INTER (0077)"),
                Line(2024, 3, 7, 28m, "Transferência Recebida - Outra Pessoa Qualquer - •••.000.000-•• - NU PAGAMENTOS - IP (0260)"));

            var received = SingleRow(MerchantKey.From("Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\""));
            Assert.False(received.Included);
            Assert.Equal(MoneyFlowType.Income, received.Type);
            Assert.Equal(SuggestionSource.OwnTransfer, received.SuggestionSource);

            var sent = SingleRow(MerchantKey.From("Transferência enviada pelo Pix - Fulano Beltrano Ciclano"));
            Assert.False(sent.Included);
            Assert.Equal(MoneyFlowType.Expense, sent.Type);
            Assert.Equal(SuggestionSource.OwnTransfer, sent.SuggestionSource);

            var stranger = SingleRow(MerchantKey.From("Transferência Recebida - Outra Pessoa Qualquer"));
            Assert.True(stranger.Included);
            Assert.Equal(MoneyFlowType.Income, stranger.Type);
            Assert.Equal(SuggestionSource.None, stranger.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldNotLookForOwnTransfers_WhenTheUsersNameHasOneWord()
        {
            SeedUser(FirstUserId, "Fulano");

            await CreateAsync(Line(2024, 3, 5, 1000m, "Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\""));

            var row = StatementImportRowSingleton.Instance.Single();
            Assert.True(row.Included);
            Assert.Equal(SuggestionSource.None, row.SuggestionSource);
        }

        [Fact]
        public async Task CreateAsync_ShouldFollowTheUsersRule_OverTheOwnTransferDetection()
        {
            const string description = "Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\"";
            SeedUser(FirstUserId, "Fulano Beltrano Ciclano");
            SeedRule(MerchantKey.From(description), Salary, hits: 2, type: MoneyFlowType.Income);

            await CreateAsync(Line(2024, 3, 5, 1000m, description));

            var row = StatementImportRowSingleton.Instance.Single();
            Assert.True(row.Included);
            Assert.Equal(new CategoryId(Salary), row.CategoryId);
            Assert.Equal(SuggestionSource.Rule, row.SuggestionSource);
        }

        /// <summary>
        /// Every investment and own-transfer line of three real statements (Inter 2025, Nubank Jan and Sep 2026), as the
        /// frontend describes them, with the holder's name, CPF and account replaced; plus each format of the other lines.
        /// </summary>
        private static readonly (string Description, decimal Amount, int Count)[] RealStatementSample =
        [
            ("Aplicacao: \"CDB PORQUINHO BANCO INTER S A\"", -21m, 8),
            ("Aplicacao: \"CDB POS DI LIQ. BANCO INTER S A\"", -100m, 2),
            ("Aplicacao: \"CDB POS DI LIQ. BANCO INTER SA\"", -100m, 8),
            ("Aplicação RDB", -500m, 4),
            ("Dinheiro guardado com resgate planejado", -500m, 1),
            ("Resgate: \"CDB POS DI LIQ. BANCO INTER S A\"", 868.29m, 13),
            ("Resgate: \"CDB PORQUINHO BANCO INTER S A\"", 50m, 3),
            ("Resgate RDB", 610m, 2),
            ("Aplicação - Estorno: \"CDB PORQUINHO FULANO CICLANO\"", 21m, 1),
            ("Credito Tesouro Direto: \"* PROV * VENDA TD NTN-B1\"", 300m, 1),
            ("Pix enviado: \"Cp :18236120-Fulano Beltrano Ciclano\"", -28m, 7),
            ("Pix recebido: \"Cp :18236120-Fulano Beltrano Ciclano\"", 1000m, 19),
            ("Pix recebido: \"Cp :90400888-FULANO BELTRANO CICLANO\"", 3167.36m, 10),
            ("Transferência enviada pelo Pix - Fulano Beltrano Ciclano - •••.000.000-•• - BANCO INTER (0077) Agência: 1 Conta: 1234-5", -96m, 3),
            ("Transferência recebida pelo Pix - FULANO BELTRANO CICLANO - •••.000.000-•• - BANCO INTER (0077) Agência: 1 Conta: 1234-5", 331.6m, 6),
            ("Compra no debito: \"No estabelecimento LOJA EXEMPLO GOIANIA BRA\"", -70.58m, 1),
            ("Loja Exemplo Goiania Bra - Compra no debito: \"No estabelecimento LOJA EXEMPLO GOIANIA BRA\"", -12m, 1),
            ("Pix enviado: \"Cp :00360305-Outra Pessoa\"", -40m, 1),
            ("Pix recebido: \"Cp :00000000-OUTRA PESSOA\"", 50m, 1),
            ("Pix enviado devolvido: \"Cp :00000000-Outra Pessoa\"", 40m, 1),
            ("Pagamento efetuado: \"Debito Automatico Fatura Cartao Inter\"", -349.14m, 1),
            ("Fatura cartão Inter - Pagamento efetuado: \"Pagamento fatura cartao Inter\"", -385.5m, 1),
            ("Pagamento de fatura", -331.6m, 1),
            ("Transferência Recebida - Outra Pessoa Qualquer - •••.000.000-•• - NU PAGAMENTOS - IP (0260) Agência: 1 Conta: 9999-9", 28m, 1)
        ];

        [Fact]
        public async Task CreateAsync_ShouldFileTheRealStatementsInvestmentsAndOwnTransfers_WhenThereIsNoHistory()
        {
            SeedUser(FirstUserId, "Fulano Beltrano Ciclano");
            var start = new DateOnly(2024, 1, 1);
            var lines = RealStatementSample
                .SelectMany(sample => Enumerable.Repeat(sample, sample.Count))
                .Select((sample, index) => new StatementLineDto(start.AddDays(index), sample.Description, sample.Amount, null))
                .ToArray();

            await CreateAsync(lines);

            var rows = StatementImportRowSingleton.Instance;
            Assert.Equal(lines.Length, rows.Count);

            var applications = rows.Where(row => row.SuggestionSource == SuggestionSource.InvestmentApplication).ToList();
            Assert.Equal(23, applications.Count);
            Assert.All(applications, row => Assert.True(row.Included && row.Type == MoneyFlowType.Investment));

            var redemptions = rows.Where(row => row.SuggestionSource == SuggestionSource.InvestmentRedemption).ToList();
            Assert.Equal(20, redemptions.Count);
            Assert.All(redemptions, row => Assert.False(row.Included));

            var ownTransfers = rows.Where(row => row.SuggestionSource == SuggestionSource.OwnTransfer).ToList();
            Assert.Equal(45, ownTransfers.Count);
            Assert.All(ownTransfers, row => Assert.False(row.Included));

            var others = rows.Where(row => row.SuggestionSource == SuggestionSource.None).ToList();
            Assert.Equal(lines.Length - 88, others.Count);
            Assert.All(others, row =>
            {
                Assert.True(row.Included);
                Assert.Equal(row.SignedAmount > 0 ? MoneyFlowType.Income : MoneyFlowType.Expense, row.Type);
            });
        }

        [Fact]
        public async Task GetByIdAsync_ShouldCountAShiftedRowInTheNextMonth()
        {
            var created = await CreateAsync(
                Line(2024, 3, 5, -40m, "LOJA"),
                Line(2024, 3, 29, 5000m, "SALARIO EMPRESA XPTO"));
            ShiftRow(SingleRow("salario empresa xpto"), Salary);

            var details = (await _service.GetByIdAsync(created.Value!.Id, FirstUserId, CancellationToken.None)).Value!;

            Assert.Equal(["2024-03", "2024-04"], details.Months.Select(month => month.Month));
            var march = details.Months[0];
            Assert.Equal((1, 0m, 40m, (int?)March2024Id), (march.RowCount, march.TotalIncome, march.TotalExpense, march.MonthlySummaryId));
            var april = details.Months[1];
            Assert.Equal((1, 5000m, (int?)null), (april.RowCount, april.TotalIncome, april.MonthlySummaryId));
        }

        [Fact]
        public async Task CommitAsync_ShouldPostAShiftedRowOnTheFirstDayOfTheNextMonth()
        {
            var created = await CreateAsync(Line(2024, 3, 29, 5000m, "SALARIO EMPRESA XPTO"));
            ShiftRow(SingleRow("salario empresa xpto"), Salary);

            var result = await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var april = Assert.Single(result.Value.Months);
            Assert.Equal(("2024-04", true), (april.Month, april.Created));
            var transaction = Assert.Single(TransactionSingleton.Instance);
            Assert.Equal(new DateOnly(2024, 4, 1), transaction.TransactionDate);
            Assert.Equal(april.MonthlySummaryId, transaction.MonthlySummaryId.Value);
            Assert.Equal(0m, MonthlySummarySingleton.Instance.Single(month => month.Id!.Value == March2024Id).TotalIncome.Value);
        }

        [Fact]
        public async Task CommitAsync_ShouldOpenJanuaryOfTheNextYear_WhenADecemberRowIsShifted()
        {
            var created = await CreateAsync(Line(2024, 12, 31, 5000m, "SALARIO EMPRESA XPTO"));
            ShiftRow(SingleRow("salario empresa xpto"), Salary);

            var result = await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal("2025-01", Assert.Single(result.Value!.Months).Month);
            Assert.Equal(new DateOnly(2025, 1, 1), Assert.Single(TransactionSingleton.Instance).TransactionDate);
        }

        [Fact]
        public async Task CommitAsync_ShouldLearnThatTheMerchantCountsInTheNextMonth()
        {
            var created = await CreateAsync(Line(2024, 3, 29, 5000m, "SALARIO EMPRESA XPTO"));
            ShiftRow(SingleRow("salario empresa xpto"), Salary);

            await _service.CommitAsync(created.Value!.Id, FirstUserId, CancellationToken.None);

            Assert.Equal(
                [(RuleAction.Categorize, (CategoryId?)new CategoryId(Salary), 1), (RuleAction.NextMonth, null, 1)],
                CategorizationRuleSingleton.Instance
                    .Where(rule => rule.MerchantKey == "salario empresa xpto" && rule.Type == MoneyFlowType.Income)
                    .OrderBy(rule => rule.Action)
                    .Select(rule => (rule.Action, rule.CategoryId, rule.Hits)));
        }

        [Fact]
        public async Task CreateAsync_ShouldShiftAMerchantsMonthEndLine_WhenTheUserShiftedItBefore()
        {
            var first = await CreateAsync(Line(2024, 2, 29, 5000m, "SALARIO EMPRESA XPTO"));
            ShiftRow(SingleRow("salario empresa xpto"), Salary);
            await _service.CommitAsync(first.Value!.Id, FirstUserId, CancellationToken.None);

            var second = await CreateAsync(
                Line(2024, 3, 15, 1200m, "SALARIO EMPRESA XPTO"),
                Line(2024, 3, 29, 5000m, "SALARIO EMPRESA XPTO"));

            var monthEnd = StatementImportRowSingleton.Instance.Single(row => row.Date == new DateOnly(2024, 3, 29));
            Assert.Equal((true, true, new DateOnly(2024, 4, 1)), (monthEnd.ShiftToNextMonth, monthEnd.ShiftLearned, monthEnd.EffectiveDate));
            Assert.False(StatementImportRowSingleton.Instance.Single(row => row.Date == new DateOnly(2024, 3, 15)).ShiftToNextMonth);

            var months = (await _service.GetByIdAsync(second.Value!.Id, FirstUserId, CancellationToken.None)).Value!.Months;
            Assert.Equal(["2024-03", "2024-04"], months.Select(month => month.Month));
        }

        [Fact]
        public async Task CreateAsync_ShouldShiftAnExpense_WhenTheUserShiftedTheMerchantsExpensesBefore()
        {
            SeedRule("fatura cartao", categoryId: null, hits: 1, action: RuleAction.NextMonth);

            await CreateAsync(Line(2024, 3, 28, -900m, "FATURA CARTAO"));

            Assert.True(SingleRow("fatura cartao").ShiftLearned);
        }

        private Task<Domain.Shared.Results.Result<StatementImportResponseDto>> CreateAsync(params StatementLineDto[] lines)
            => _service.CreateAsync(new StatementImportDto("extrato.ofx", StatementImportSource.Ofx, Account, lines), FirstUserId, CancellationToken.None);

        private static StatementLineDto Line(int year, int month, int day, decimal amount, string description, string? fitid = null)
            => new(new DateOnly(year, month, day), description, amount, fitid);

        private static StatementImportRow SingleRow(string merchantKey)
            => StatementImportRowSingleton.Instance.Single(row => row.MerchantKey == merchantKey);

        /// <summary>Files the draft rows of a merchant as the user would, through the row's own rules.</summary>
        private static Task FileRowAsync(string merchantKey, int? categoryId, MoneyFlowType? type = null, bool included = true)
        {
            foreach (var row in StatementImportRowSingleton.Instance.Where(row => row.MerchantKey == merchantKey).ToList())
                ReplaceRow(row, edited => edited.Edit(type ?? edited.Type, categoryId, edited.Amount.Value, edited.Description.Value, edited.Date, included, Today));

            return Task.CompletedTask;
        }

        private static Task FileAllRowsAsync(int categoryId)
        {
            foreach (var row in StatementImportRowSingleton.Instance.ToList())
                ReplaceRow(row, edited => edited.EditInBulk(type: null, idCategory: categoryId, included: null));

            return Task.CompletedTask;
        }

        /// <summary>Counts the row in the month after its date, as the user would on the review screen.</summary>
        private static void ShiftRow(StatementImportRow row, int categoryId)
            => ReplaceRow(row, edited => edited.Edit(edited.Type, categoryId, edited.Amount.Value, edited.Description.Value, edited.Date, included: true, Today, shiftToNextMonth: true));

        private static void ReplaceRow(StatementImportRow row, Func<StatementImportRow, Domain.Shared.Results.Result<StatementImportRow>> edit)
        {
            var copy = StatementImportRepositoryMock.ToDetachedCopy(row);
            var result = edit(copy);
            Assert.True(result.IsSuccess);

            var rows = StatementImportRowSingleton.Instance;
            rows[rows.FindIndex(stored => stored.Id == row.Id)] = copy;
        }

        private static void SeedUser(UserId userId, string name)
        {
            var user = User.Create(name, $"user{userId.Value}@example.com", "hash").Value!;
            user.AssignId(userId.Value);
            UserSingleton.Instance.Add(user);
        }

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }

        private static void SeedRule(string merchantKey, int? categoryId, int hits, MoneyFlowType type = MoneyFlowType.Expense, RuleAction action = RuleAction.Categorize, UserId? userId = null)
        {
            var rules = CategorizationRuleSingleton.Instance;
            rules.Add(CategorizationRule.FromPersistence(rules.Count + 1, (userId ?? FirstUserId).Value, merchantKey, type, action, categoryId, hits, Today));
        }

        private static Transaction SeedTransaction(int monthlySummaryId, MoneyFlowType type, int categoryId, decimal amount, string description, DateOnly date)
        {
            var transactions = TransactionSingleton.Instance;
            var transaction = Transaction.FromPersistence(transactions.Count + 1, monthlySummaryId, type, categoryId, amount, description, date);
            transactions.Add(transaction);

            return transaction;
        }
    }
}
