using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Transactions.Mocks;
using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Services;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Domain.Transactions.Errors;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Transactions
{
    [Collection("ApplicationServices")]
    public class TransactionServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

        // Month 1 and categories 1-3 belong to the first user; month 2 and category 4 to the second.
        private const int MonthId = 1;
        private const int OtherUserMonthId = 2;
        private const int MarketCategoryId = 1;
        private const int SalaryCategoryId = 2;
        private const int HealthCategoryId = 3;
        private const int OtherUserCategoryId = 4;

        private readonly TransactionService _transactionService;
        private readonly TransactionRepositoryMock _transactionRepository;

        public TransactionServiceTest()
        {
            _transactionService = ServiceProvider.GetRequiredService<TransactionService>();
            _transactionRepository = (TransactionRepositoryMock)ServiceProvider.GetRequiredService<ITransactionRepository>();

            TransactionSingleton.Instance.Clear();
            MonthlySummarySingleton.Instance.Clear();
            CategorySingleton.Instance.Clear();

            MonthlySummarySingleton.Instance.AddRange(
                MonthlySummary.FromPersistence(MonthId, FirstUserId.Value, 3, CurrentYear, 0, 0),
                MonthlySummary.FromPersistence(OtherUserMonthId, SecondUserId.Value, 3, CurrentYear, 0, 0));

            SeedCategory(MarketCategoryId, FirstUserId, "Mercado");
            SeedCategory(SalaryCategoryId, FirstUserId, "Salário");
            SeedCategory(HealthCategoryId, FirstUserId, "Saúde");
            SeedCategory(OtherUserCategoryId, SecondUserId, "Outros");
        }

        [Fact]
        public async Task CreateAsync_ShouldAddTransactionAndUpdateMonthTotals_WhenValuesAreValid()
        {
            var result = await _transactionService.CreateAsync(MonthId, Expense("  Supermercado  ", 120.50m, 7), FirstUserId, CancellationToken.None);
            await _transactionService.CreateAsync(MonthId, Income("Salário", 1000m, 5), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                new TransactionResponseDto(1, MoneyFlowType.Expense, MarketCategoryId, "Mercado", 120.50m, "Supermercado", Day(7)),
                result.Value);
            AssertMonthTotals(1000m, 120.50m);
        }

        [Fact]
        public async Task CreateAsync_ShouldUpdateTotals_WhenFirstTransactionOfEmptyMonthIsIncome()
        {
            var result = await _transactionService.CreateAsync(MonthId, Income("Salário", 8000m, 1), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            AssertMonthTotals(8000m, 0m);
        }

        [Fact]
        public async Task DeleteAsync_ShouldZeroTotals_WhenLastTransactionIsDeleted()
        {
            var created = await Create(Expense("Supermercado", 8000m, 1));

            var result = await _transactionService.DeleteAsync(MonthId, created.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            AssertMonthTotals(0m, 0m);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnNotFound_WhenMonthBelongsToAnotherUser()
        {
            var result = await _transactionService.CreateAsync(OtherUserMonthId, Expense("Supermercado", 10m, 7), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.NotFound, result.Error);
            Assert.Empty(TransactionSingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnCategoryNotFound_WhenCategoryBelongsToAnotherUser()
        {
            var dto = new TransactionDto(MoneyFlowType.Expense, OtherUserCategoryId, 10m, "Supermercado", Day(7));

            var result = await _transactionService.CreateAsync(MonthId, dto, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal(0, _transactionRepository.AddCallCount);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        public async Task CreateAsync_ShouldReturnDateOutsideMonth_WhenDateIsNotInTheMonth(int monthOffset)
        {
            var dto = new TransactionDto(MoneyFlowType.Expense, MarketCategoryId, 10m, "Supermercado", Day(1).AddMonths(monthOffset));

            var result = await _transactionService.CreateAsync(MonthId, dto, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.DateOutsideMonth, result.Error);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Equal(0, _transactionRepository.AddCallCount);
        }

        [Fact]
        public async Task UpdateAsync_ShouldMoveAmountBetweenTotals_WhenTypeChanges()
        {
            var created = await Create(Income("Freelance", 300m, 10));

            var result = await _transactionService.UpdateAsync(
                MonthId, created.Id, new TransactionDto(MoneyFlowType.Expense, HealthCategoryId, 250m, "Farmácia", Day(11)), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                new TransactionResponseDto(created.Id, MoneyFlowType.Expense, HealthCategoryId, "Saúde", 250m, "Farmácia", Day(11)),
                result.Value);
            AssertMonthTotals(0m, 250m);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_WhenTransactionIsInAnotherMonth()
        {
            var created = await Create(Expense("Supermercado", 10m, 7));

            var result = await _transactionService.UpdateAsync(OtherUserMonthId, created.Id, Expense("Feira", 20m, 7), SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.NotFound, result.Error);
            Assert.Equal(0, _transactionRepository.UpdateCallCount);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnValidationError_WhenAmountIsInvalid()
        {
            var created = await Create(Expense("Supermercado", 10m, 7));

            var result = await _transactionService.UpdateAsync(MonthId, created.Id, Expense("Supermercado", 0m, 7), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.AmountNotPositive, result.Error);
            AssertMonthTotals(0m, 10m);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveTransactionAndRecalculateTotals()
        {
            var removed = await Create(Expense("Supermercado", 10m, 7));
            await Create(Expense("Feira", 5m, 8));

            var result = await _transactionService.DeleteAsync(MonthId, removed.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Single(TransactionSingleton.Instance);
            AssertMonthTotals(0m, 5m);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFound_WhenTransactionDoesNotExist()
        {
            var result = await _transactionService.DeleteAsync(MonthId, 99, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(TransactionErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnTransactionWithCategoryName()
        {
            var created = await Create(Expense("Supermercado", 10m, 7));

            var result = await _transactionService.GetByIdAsync(MonthId, created.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(created, result.Value);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnNewestFirst_ByDefault()
        {
            await Create(Expense("Aluguel", 1000m, 5));
            await Create(Expense("Supermercado", 200m, 20));
            await Create(Income("Salário", 3000m, 5));

            var result = await _transactionService.GetPagedAsync(MonthId, new TransactionListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.TotalCount);
            Assert.Equal([2, 3, 1], result.Value.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSearchIgnoringCaseAndAccents()
        {
            await Create(Expense("Farmácia", 50m, 3));
            await Create(Expense("Supermercado", 200m, 4));

            var query = new TransactionListQueryDto { Search = "  FARMACIA " };
            var result = await _transactionService.GetPagedAsync(MonthId, query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Farmácia", Assert.Single(result.Value.Items).Description);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFilterByTypeAndCategory()
        {
            await Create(Expense("Supermercado", 200m, 4));
            await Create(new TransactionDto(MoneyFlowType.Expense, HealthCategoryId, 50m, "Farmácia", Day(3)));
            await Create(Income("Salário", 3000m, 5));

            var incomes = await _transactionService.GetPagedAsync(MonthId, new TransactionListQueryDto { Type = TransactionTypeFilter.Income }, FirstUserId, CancellationToken.None);
            var health = await _transactionService.GetPagedAsync(MonthId, new TransactionListQueryDto { CategoryId = HealthCategoryId }, FirstUserId, CancellationToken.None);

            Assert.Equal([3], incomes.Value!.Items.Select(item => item.Id));
            Assert.Equal([2], health.Value!.Items.Select(item => item.Id));
        }

        [Theory]
        [InlineData(TransactionSortBy.Description, SortDirection.Asc, new[] { 2, 3, 1 })]
        [InlineData(TransactionSortBy.Description, SortDirection.Desc, new[] { 1, 3, 2 })]
        [InlineData(TransactionSortBy.Category, SortDirection.Asc, new[] { 1, 3, 2 })]
        [InlineData(TransactionSortBy.Category, SortDirection.Desc, new[] { 2, 3, 1 })]
        [InlineData(TransactionSortBy.Amount, SortDirection.Desc, new[] { 3, 2, 1 })]
        [InlineData(TransactionSortBy.Amount, SortDirection.Asc, new[] { 1, 2, 3 })]
        [InlineData(TransactionSortBy.Date, SortDirection.Asc, new[] { 2, 3, 1 })]
        public async Task GetPagedAsync_ShouldSortByColumn(TransactionSortBy sortBy, SortDirection sortDirection, int[] expectedIds)
        {
            await Create(Expense("Supermercado", 200m, 20));                                                             // Mercado, -200
            await Create(new TransactionDto(MoneyFlowType.Expense, HealthCategoryId, 50m, "Academia", Day(3)));          // Saúde, -50
            await Create(Income("Freelance", 900m, 10));                                                                 // Salário, +900

            var query = new TransactionListQueryDto { SortBy = sortBy, SortDirection = sortDirection };
            var result = await _transactionService.GetPagedAsync(MonthId, query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(expectedIds, result.Value.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnRequestedPage()
        {
            for (var day = 1; day <= 5; day++)
                await Create(Expense($"Gasto {day}", day, day));

            var query = new TransactionListQueryDto { Page = 2, PageSize = 2, SortDirection = SortDirection.Asc };
            var result = await _transactionService.GetPagedAsync(MonthId, query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal([3, 4], result.Value.Items.Select(item => item.Id));
            Assert.Equal(5, result.Value.TotalCount);
            Assert.Equal(3, result.Value.TotalPages);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnEmptyItems_WhenPageIsBeyondTheLast()
        {
            await Create(Expense("Supermercado", 10m, 7));

            var result = await _transactionService.GetPagedAsync(MonthId, new TransactionListQueryDto { Page = 5 }, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(1, result.Value.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnNotFound_WhenMonthBelongsToAnotherUser()
        {
            var result = await _transactionService.GetPagedAsync(OtherUserMonthId, new TransactionListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.NotFound, result.Error);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 0)]
        [InlineData(1, PageRequest.MaxPageSize + 1)]
        public async Task GetPagedAsync_ShouldReturnValidationError_WhenPagingIsInvalid(int page, int pageSize)
        {
            var query = new TransactionListQueryDto { Page = page, PageSize = pageSize };
            var result = await _transactionService.GetPagedAsync(MonthId, query, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(page < 1 ? PagingErrors.InvalidPage : PagingErrors.InvalidPageSize, result.Error);
        }

        private static DateOnly Day(int day) => new(CurrentYear, 3, day);

        private static TransactionDto Expense(string description, decimal amount, int day)
            => new(MoneyFlowType.Expense, MarketCategoryId, amount, description, Day(day));

        private static TransactionDto Income(string description, decimal amount, int day)
            => new(MoneyFlowType.Income, SalaryCategoryId, amount, description, Day(day));

        private async Task<TransactionResponseDto> Create(TransactionDto dto)
        {
            var result = await _transactionService.CreateAsync(MonthId, dto, FirstUserId, CancellationToken.None);

            return result.Value!;
        }

        private static void AssertMonthTotals(decimal totalIncome, decimal totalExpense)
        {
            var month = MonthlySummarySingleton.Instance.Single(monthlySummary => monthlySummary.Id!.Value == MonthId);

            Assert.Equal(totalIncome, month.TotalIncome.Value);
            Assert.Equal(totalExpense, month.TotalExpense.Value);
            Assert.Equal(totalIncome - totalExpense, month.BalanceAmount);
        }

        private static void SeedCategory(int id, UserId userId, string name)
        {
            var category = Category.Create(userId.Value, name).Value!;
            category.AssignId(id);
            CategorySingleton.Instance.Add(category);
        }
    }
}
