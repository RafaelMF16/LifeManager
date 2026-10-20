using LifeManager.Application.StatementImports.DTOs;
using LifeManager.Application.StatementImports.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.StatementImports.Errors;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.StatementImports
{
    [Collection("ApplicationServices")]
    public class StatementImportRowServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly DateOnly Today = new(2026, 10, 19);

        private const int Restaurants = 1;
        private const int Market = 2;
        private const int OtherUserCategory = 5;

        private readonly StatementImportService _importService;
        private readonly StatementImportRowService _service;

        public StatementImportRowServiceTest()
        {
            _importService = ServiceProvider.GetRequiredService<StatementImportService>();
            _service = ServiceProvider.GetRequiredService<StatementImportRowService>();
            ((FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>()).SetToday(Today);

            StatementImportSingleton.Instance.Clear();
            StatementImportRowSingleton.Instance.Clear();
            CategorizationRuleSingleton.Instance.Clear();
            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();

            SeedCategory(Restaurants, FirstUserId, "Restaurantes");
            SeedCategory(Market, FirstUserId, "Mercado");
            SeedCategory(OtherUserCategory, SecondUserId, "Outros");
        }

        // ---------- Listing ----------

        [Fact]
        public async Task GetPagedAsync_ShouldListTheRowsByDateWithTheirCategoryName()
        {
            var importId = await CreateDraftAsync(
                Line(2024, 3, 7, -10m, "PADARIA"),
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"));
            await UpdateAsync(importId, RowId("ifood restaurante"), Restaurants);

            var result = await _service.GetPagedAsync(importId, new StatementImportRowListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(["IFOOD *RESTAURANTE", "PADARIA"], result.Value.Items.Select(row => row.OriginalDescription));
            Assert.Equal("Restaurantes", result.Value.Items[0].CategoryName);
            Assert.Null(result.Value.Items[1].CategoryName);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFilterByMonthAndStatus()
        {
            CategorizationRuleSingleton.Instance.Add(CategorizationRule.FromPersistence(1, FirstUserId.Value, "padaria", MoneyFlowType.Expense, RuleAction.Categorize, Market, 3, Today));
            var importId = await CreateDraftAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -10m, "PADARIA"),
                Line(2024, 4, 6, -12m, "PADARIA"),
                Line(2024, 4, 9, -99m, "LOJA"));
            await UpdateAsync(importId, RowId("loja"), categoryId: null, included: false);

            Assert.Equal(["IFOOD *RESTAURANTE", "PADARIA"], await DescriptionsAsync(importId, new() { Month = "2024-03" }));
            Assert.Equal(["IFOOD *RESTAURANTE"], await DescriptionsAsync(importId, new() { Filter = StatementImportRowFilter.Uncategorized }));
            Assert.Equal(["PADARIA", "PADARIA"], await DescriptionsAsync(importId, new() { Filter = StatementImportRowFilter.Suggested }));
            Assert.Equal(["LOJA"], await DescriptionsAsync(importId, new() { Filter = StatementImportRowFilter.Excluded }));
            Assert.Equal(["PADARIA"], await DescriptionsAsync(importId, new() { Month = "2024-04", Filter = StatementImportRowFilter.Suggested }));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSearchWithoutAccentsOrCase()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "Açaí da Praça"), Line(2024, 3, 6, -10m, "PADARIA"));

            Assert.Equal(["Açaí da Praça"], await DescriptionsAsync(importId, new() { Search = "ACAI" }));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortByAmountDescending()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"), Line(2024, 3, 6, 100m, "B"), Line(2024, 3, 7, -10m, "C"));

            Assert.Equal(["B", "C", "A"], await DescriptionsAsync(importId, new() { SortBy = StatementImportRowSortBy.Amount, SortDirection = SortDirection.Desc }));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldPage()
        {
            var importId = await CreateDraftAsync([.. Enumerable.Range(1, 5).Select(day => Line(2024, 3, day, -1m, $"Linha {day}"))]);

            var result = await _service.GetPagedAsync(importId, new StatementImportRowListQueryDto { Page = 2, PageSize = 2 }, FirstUserId, CancellationToken.None);

            Assert.Equal(["Linha 3", "Linha 4"], result.Value!.Items.Select(row => row.Description));
            Assert.Equal(5, result.Value.TotalCount);
            Assert.Equal(3, result.Value.TotalPages);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFail_WhenTheMonthIsInvalid()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.GetPagedAsync(importId, new StatementImportRowListQueryDto { Month = "03/2024" }, FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.InvalidMonth, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFail_WhenThePageIsInvalid()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.GetPagedAsync(importId, new StatementImportRowListQueryDto { Page = 0 }, FirstUserId, CancellationToken.None);

            Assert.Equal(PagingErrors.InvalidPage, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnNotFound_ForAnotherUsersImport()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.GetPagedAsync(importId, new StatementImportRowListQueryDto(), SecondUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NotFound, result.Error);
        }

        // ---------- Update ----------

        [Fact]
        public async Task UpdateAsync_ShouldSaveTheEditAndCountSimilarRowsFiledDifferently()
        {
            var importId = await CreateDraftAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -45m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 7, -50m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 8, -10m, "PADARIA"));
            var rowIds = RowIds("ifood restaurante");

            var result = await _service.UpdateAsync(importId, rowIds[0], RowDto(Restaurants, "Almoço"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.SimilarCount);
            Assert.Equal("Almoço", result.Value.Row.Description);
            Assert.Equal("Restaurantes", result.Value.Row.CategoryName);
            Assert.True(result.Value.Row.EditedByUser);

            var stored = StatementImportRowSingleton.Instance.Single(row => row.Id!.Value == rowIds[0]);
            Assert.Equal(new CategoryId(Restaurants), stored.CategoryId);
        }

        [Fact]
        public async Task UpdateAsync_ShouldMoveTheRowToAnotherMonth()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 31, -40m, "LOJA"));

            await _service.UpdateAsync(importId, RowId("loja"), RowDto(Market, "Loja", date: new DateOnly(2024, 4, 1)), FirstUserId, CancellationToken.None);

            var months = (await _importService.GetByIdAsync(importId, FirstUserId, CancellationToken.None)).Value!.Months;
            Assert.Equal(["2024-04"], months.Select(month => month.Month));
        }

        [Fact]
        public async Task UpdateAsync_ShouldFail_WhenTheCategoryIsNotTheUsers()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "LOJA"));

            var result = await _service.UpdateAsync(importId, RowId("loja"), RowDto(OtherUserCategory, "Loja"), FirstUserId, CancellationToken.None);

            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Null(SingleRow("loja").CategoryId);
        }

        [Fact]
        public async Task UpdateAsync_ShouldFail_WhenTheDescriptionIsTooLong()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "LOJA"));

            var result = await _service.UpdateAsync(importId, RowId("loja"), RowDto(Market, new string('a', 81)), FirstUserId, CancellationToken.None);

            Assert.Equal(TransactionErrors.DescriptionTooLong, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnRowNotFound_ForARowOfAnotherImport()
        {
            var firstImportId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "LOJA"));
            var otherRowId = RowId("loja");
            var secondImportId = await CreateDraftAsync(Line(2024, 3, 6, -10m, "PADARIA"));

            var result = await _service.UpdateAsync(secondImportId, otherRowId, RowDto(Market, "Loja"), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.RowNotFound, result.Error);
            Assert.NotEqual(firstImportId, secondImportId);
        }

        [Fact]
        public async Task UpdateAsync_ShouldFail_OnceTheImportIsCommitted()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "LOJA"), Line(2024, 3, 6, -10m, "PADARIA"));
            var padariaId = RowId("padaria");
            await UpdateAsync(importId, RowId("loja"), Market);
            await UpdateAsync(importId, padariaId, Market);
            await _importService.CommitAsync(importId, FirstUserId, CancellationToken.None);

            var result = await _service.UpdateAsync(importId, padariaId, RowDto(Restaurants, "Padaria"), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.AlreadyCommitted, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_ForAnotherUsersImport()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "LOJA"));

            var result = await _service.UpdateAsync(importId, RowId("loja"), RowDto(OtherUserCategory, "Loja"), SecondUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NotFound, result.Error);
        }

        // ---------- Bulk ----------

        [Fact]
        public async Task BulkUpdateAsync_ShouldChangeOnlyTheSelectedRowsAndValues()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"), Line(2024, 3, 6, -10m, "B"), Line(2024, 3, 7, -12m, "C"));
            var selected = new[] { RowId("a"), RowId("b") };

            var result = await _service.BulkUpdateAsync(importId, new StatementImportBulkUpdateDto(selected, Type: null, CategoryId: Market, Included: null), FirstUserId, CancellationToken.None);

            Assert.Equal(2, result.Value!.UpdatedCount);
            Assert.Equal(new CategoryId(Market), SingleRow("a").CategoryId);
            Assert.Equal(new CategoryId(Market), SingleRow("b").CategoryId);
            Assert.Null(SingleRow("c").CategoryId);
            Assert.All(StatementImportRowSingleton.Instance, row => Assert.True(row.Included));
        }

        [Fact]
        public async Task BulkUpdateAsync_ShouldFail_WhenNothingIsChosen()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.BulkUpdateAsync(importId, new StatementImportBulkUpdateDto([RowId("a")], null, null, null), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.NothingToChange, result.Error);
        }

        [Fact]
        public async Task BulkUpdateAsync_ShouldFail_WhenTooManyRowsAreSelected()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.BulkUpdateAsync(
                importId, new StatementImportBulkUpdateDto([.. Enumerable.Range(1, 101)], null, null, Included: false), FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.TooManyRowsSelected, result.Error);
        }

        [Fact]
        public async Task BulkUpdateAsync_ShouldFail_WhenTheCategoryIsNotTheUsers()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.BulkUpdateAsync(importId, new StatementImportBulkUpdateDto([RowId("a")], null, OtherUserCategory, null), FirstUserId, CancellationToken.None);

            Assert.Equal(CategoryErrors.NotFound, result.Error);
        }

        // ---------- Apply to similar ----------

        [Fact]
        public async Task ApplyToSimilarAsync_ShouldFileTheSameMerchantsRowsLikeTheSource()
        {
            var importId = await CreateDraftAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -45m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 7, -50m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 8, -10m, "PADARIA"));
            var rowIds = RowIds("ifood restaurante");
            await UpdateAsync(importId, rowIds[0], Restaurants);

            var result = await _service.ApplyToSimilarAsync(importId, rowIds[0], FirstUserId, CancellationToken.None);

            Assert.Equal(2, result.Value!.UpdatedCount);
            Assert.All(StatementImportRowSingleton.Instance.Where(row => row.MerchantKey == "ifood restaurante"),
                row => Assert.Equal(new CategoryId(Restaurants), row.CategoryId));
            Assert.Null(SingleRow("padaria").CategoryId);
        }

        [Fact]
        public async Task ApplyToSimilarAsync_ShouldKeepRowsTheUserEditedAndDuplicatesOut()
        {
            var importId = await CreateDraftAsync(
                Line(2024, 3, 5, -40m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 6, -45m, "IFOOD *RESTAURANTE"),
                Line(2024, 3, 7, -50m, "IFOOD *RESTAURANTE"));
            var rowIds = RowIds("ifood restaurante");
            await UpdateAsync(importId, rowIds[1], Market);
            MarkDuplicate(rowIds[2]);
            await UpdateAsync(importId, rowIds[0], Restaurants);

            var result = await _service.ApplyToSimilarAsync(importId, rowIds[0], FirstUserId, CancellationToken.None);

            Assert.Equal(1, result.Value!.UpdatedCount);
            Assert.Equal(new CategoryId(Market), Row(rowIds[1]).CategoryId);
            Assert.Equal(new CategoryId(Restaurants), Row(rowIds[2]).CategoryId);
            Assert.False(Row(rowIds[2]).Included);
        }

        [Fact]
        public async Task ApplyToSimilarAsync_ShouldLeaveTheRowsOut_WhenTheSourceIsLeftOut()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -500m, "PIX ENVIADO JOAO"), Line(2024, 4, 5, -500m, "PIX ENVIADO JOAO"));
            var rowIds = RowIds("joao");
            await UpdateAsync(importId, rowIds[0], categoryId: null, included: false);

            var result = await _service.ApplyToSimilarAsync(importId, rowIds[0], FirstUserId, CancellationToken.None);

            Assert.Equal(1, result.Value!.UpdatedCount);
            Assert.False(Row(rowIds[1]).Included);
        }

        [Fact]
        public async Task ApplyToSimilarAsync_ShouldReturnRowNotFound_WhenTheRowDoesNotExist()
        {
            var importId = await CreateDraftAsync(Line(2024, 3, 5, -40m, "A"));

            var result = await _service.ApplyToSimilarAsync(importId, 999, FirstUserId, CancellationToken.None);

            Assert.Equal(StatementImportErrors.RowNotFound, result.Error);
        }

        // ---------- Helpers ----------

        private async Task<int> CreateDraftAsync(params StatementLineDto[] lines)
        {
            var result = await _importService.CreateAsync(new StatementImportDto("extrato.csv", StatementImportSource.Csv, null, lines), FirstUserId, CancellationToken.None);
            Assert.True(result.IsSuccess);

            return result.Value.Id;
        }

        private async Task UpdateAsync(int importId, int rowId, int? categoryId, bool included = true)
        {
            var row = Row(rowId);
            var result = await _service.UpdateAsync(
                importId, rowId, new StatementImportRowDto(row.Type, categoryId, row.Amount.Value, row.Description.Value, row.Date, included), FirstUserId, CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        private async Task<string[]> DescriptionsAsync(int importId, StatementImportRowListQueryDto query)
        {
            var result = await _service.GetPagedAsync(importId, query, FirstUserId, CancellationToken.None);
            Assert.True(result.IsSuccess);

            return [.. result.Value.Items.Select(row => row.OriginalDescription)];
        }

        private static StatementImportRowDto RowDto(int? categoryId, string description, DateOnly? date = null)
            => new(MoneyFlowType.Expense, categoryId, 40m, description, date ?? new DateOnly(2024, 3, 5), Included: true);

        private static StatementLineDto Line(int year, int month, int day, decimal amount, string description)
            => new(new DateOnly(year, month, day), description, amount, Fitid: null);

        private static StatementImportRow SingleRow(string merchantKey)
            => StatementImportRowSingleton.Instance.Single(row => row.MerchantKey == merchantKey);

        private static StatementImportRow Row(int rowId)
            => StatementImportRowSingleton.Instance.Single(row => row.Id!.Value == rowId);

        private static int RowId(string merchantKey) => SingleRow(merchantKey).Id!.Value;

        private static int[] RowIds(string merchantKey)
            => [.. StatementImportRowSingleton.Instance.Where(row => row.MerchantKey == merchantKey).OrderBy(row => row.Date).Select(row => row.Id!.Value)];

        private static void MarkDuplicate(int rowId)
        {
            var rows = StatementImportRowSingleton.Instance;
            var index = rows.FindIndex(row => row.Id!.Value == rowId);
            var copy = Mocks.StatementImportRepositoryMock.ToDetachedCopy(rows[index]);
            copy.MarkDuplicateOf(transactionId: 77, monthlySummaryId: 7);
            rows[index] = copy;
        }

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }
    }
}
