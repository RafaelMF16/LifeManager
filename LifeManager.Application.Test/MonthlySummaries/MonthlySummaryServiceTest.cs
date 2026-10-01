using LifeManager.Application.MonthlySummaries.DTOs;
using LifeManager.Application.MonthlySummaries.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.MonthlySummaries.Mocks;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.MonthlySummaries.Enums;
using LifeManager.Domain.MonthlySummaries.Errors;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.MonthlySummaries
{
    [Collection("ApplicationServices")]
    public class MonthlySummaryServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

        private readonly MonthlySummaryService _monthlySummaryService;
        private readonly MonthlySummaryRepositoryMock _monthlySummaryRepository;

        public MonthlySummaryServiceTest()
        {
            _monthlySummaryService = ServiceProvider.GetRequiredService<MonthlySummaryService>();
            _monthlySummaryRepository = (MonthlySummaryRepositoryMock)ServiceProvider.GetRequiredService<IMonthlySummaryRepository>();

            MonthlySummarySingleton.Instance.Clear();
        }

        [Fact]
        public async Task CreateAsync_ShouldAddMonthInCurrentYearWithZeroTotals_WhenMonthIsValid()
        {
            var result = await _monthlySummaryService.CreateAsync(new MonthlySummaryDto(3), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(new MonthlySummaryResponseDto(1, 3, CurrentYear, 0, 0, 0), result.Value);
            var stored = Assert.Single(MonthlySummarySingleton.Instance);
            Assert.Equal(FirstUserId, stored.UserId);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        public async Task CreateAsync_ShouldReturnInvalidMonth_WhenMonthIsOutOfRange(int month)
        {
            var result = await _monthlySummaryService.CreateAsync(new MonthlySummaryDto(month), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.InvalidMonth, result.Error);
            Assert.Equal(0, _monthlySummaryRepository.ExistsCallCount);
            Assert.Empty(MonthlySummarySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnAlreadyExists_WhenMonthWasAlreadyCreated()
        {
            await _monthlySummaryService.CreateAsync(new MonthlySummaryDto(3), FirstUserId, CancellationToken.None);

            var result = await _monthlySummaryService.CreateAsync(new MonthlySummaryDto(3), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.AlreadyExists, result.Error);
            Assert.Single(MonthlySummarySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowSameMonth_WhenItBelongsToAnotherUser()
        {
            await _monthlySummaryService.CreateAsync(new MonthlySummaryDto(3), FirstUserId, CancellationToken.None);

            var result = await _monthlySummaryService.CreateAsync(new MonthlySummaryDto(3), SecondUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, MonthlySummarySingleton.Instance.Count);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenMonthBelongsToAnotherUser()
        {
            Seed(Stored(1, SecondUserId.Value, 1, CurrentYear));

            var result = await _monthlySummaryService.GetByIdAsync(1, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(MonthlySummaryErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnOnlyUserMonthsNewestFirst_ByDefault()
        {
            Seed(
                Stored(1, FirstUserId.Value, 12, CurrentYear - 1),
                Stored(2, FirstUserId.Value, 2, CurrentYear),
                Stored(3, FirstUserId.Value, 1, CurrentYear),
                Stored(4, SecondUserId.Value, 5, CurrentYear));

            var result = await _monthlySummaryService.GetPagedAsync(new MonthlySummaryListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(3, result.Value.TotalCount);
            Assert.Equal([2, 3, 1], result.Value.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldSortOldestFirst_WhenPeriodIsAsc()
        {
            Seed(
                Stored(1, FirstUserId.Value, 2, CurrentYear),
                Stored(2, FirstUserId.Value, 12, CurrentYear - 1),
                Stored(3, FirstUserId.Value, 1, CurrentYear));

            var query = new MonthlySummaryListQueryDto { SortBy = MonthlySummarySortBy.Period, SortDirection = SortDirection.Asc };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal([2, 3, 1], result.Value!.Items.Select(item => item.Id));
        }

        [Theory]
        [InlineData(MonthlySummarySortBy.TotalIncome, SortDirection.Desc, new[] { 2, 3, 1 })]
        [InlineData(MonthlySummarySortBy.TotalIncome, SortDirection.Asc, new[] { 1, 3, 2 })]
        [InlineData(MonthlySummarySortBy.TotalExpense, SortDirection.Desc, new[] { 1, 2, 3 })]
        [InlineData(MonthlySummarySortBy.TotalExpense, SortDirection.Asc, new[] { 3, 2, 1 })]
        [InlineData(MonthlySummarySortBy.Balance, SortDirection.Desc, new[] { 3, 2, 1 })]
        [InlineData(MonthlySummarySortBy.Balance, SortDirection.Asc, new[] { 1, 2, 3 })]
        public async Task GetPagedAsync_ShouldSortByAmountColumn(MonthlySummarySortBy sortBy, SortDirection sortDirection, int[] expectedIds)
        {
            Seed(
                Stored(1, FirstUserId.Value, 1, CurrentYear, totalIncome: 100, totalExpense: 900),
                Stored(2, FirstUserId.Value, 2, CurrentYear, totalIncome: 800, totalExpense: 500),
                Stored(3, FirstUserId.Value, 3, CurrentYear, totalIncome: 400, totalExpense: 0));

            var query = new MonthlySummaryListQueryDto { SortBy = sortBy, SortDirection = sortDirection };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal(expectedIds, result.Value!.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldBreakTiesChronologically_WhenAmountsAreEqual()
        {
            Seed(
                Stored(1, FirstUserId.Value, 1, CurrentYear),
                Stored(2, FirstUserId.Value, 3, CurrentYear),
                Stored(3, FirstUserId.Value, 2, CurrentYear));

            var query = new MonthlySummaryListQueryDto { SortBy = MonthlySummarySortBy.Balance, SortDirection = SortDirection.Desc };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal([2, 3, 1], result.Value!.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldFilterByYear()
        {
            Seed(
                Stored(1, FirstUserId.Value, 12, CurrentYear - 1),
                Stored(2, FirstUserId.Value, 1, CurrentYear));

            var query = new MonthlySummaryListQueryDto { Year = CurrentYear - 1 };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            var item = Assert.Single(result.Value!.Items);
            Assert.Equal(1, item.Id);
        }

        [Theory]
        [InlineData(BalanceFilter.All, new[] { 3, 2, 1 })]
        [InlineData(BalanceFilter.Positive, new[] { 3, 2 })]
        [InlineData(BalanceFilter.Negative, new[] { 1 })]
        public async Task GetPagedAsync_ShouldFilterByBalance(BalanceFilter balanceFilter, int[] expectedIds)
        {
            Seed(
                Stored(1, FirstUserId.Value, 1, CurrentYear, totalIncome: 100, totalExpense: 200),
                Stored(2, FirstUserId.Value, 2, CurrentYear),
                Stored(3, FirstUserId.Value, 3, CurrentYear, totalIncome: 300, totalExpense: 100));

            var query = new MonthlySummaryListQueryDto { Balance = balanceFilter };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal(expectedIds, result.Value!.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnRequestedPage()
        {
            for (var month = 1; month <= 5; month++)
                Seed(Stored(month, FirstUserId.Value, month, CurrentYear));

            var query = new MonthlySummaryListQueryDto { Page = 2, PageSize = 2 };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal(5, result.Value!.TotalCount);
            Assert.Equal(3, result.Value.TotalPages);
            Assert.Equal([3, 2], result.Value.Items.Select(item => item.Id));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnEmptyItems_WhenPageIsBeyondTheLast()
        {
            Seed(Stored(1, FirstUserId.Value, 1, CurrentYear));

            var query = new MonthlySummaryListQueryDto { Page = 5, PageSize = 10 };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(1, result.Value.TotalCount);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(1, 0)]
        [InlineData(1, PageRequest.MaxPageSize + 1)]
        public async Task GetPagedAsync_ShouldReturnValidationError_WhenPagingIsInvalid(int page, int pageSize)
        {
            var query = new MonthlySummaryListQueryDto { Page = page, PageSize = pageSize };
            var result = await _monthlySummaryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(page < 1 ? PagingErrors.InvalidPage : PagingErrors.InvalidPageSize, result.Error);
        }

        [Fact]
        public async Task GetYearsAsync_ShouldReturnDistinctUserYearsNewestFirst()
        {
            Seed(
                Stored(1, FirstUserId.Value, 1, CurrentYear - 2),
                Stored(2, FirstUserId.Value, 1, CurrentYear),
                Stored(3, FirstUserId.Value, 2, CurrentYear),
                Stored(4, SecondUserId.Value, 1, CurrentYear - 1));

            var result = await _monthlySummaryService.GetYearsAsync(FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal([CurrentYear, CurrentYear - 2], result.Value);
        }

        /// <summary>A summary as stored in the database: any year and any totals, unlike <see cref="MonthlySummary.Create"/>.</summary>
        private static MonthlySummary Stored(int id, int userId, int month, int year, decimal totalIncome = 0, decimal totalExpense = 0)
            => MonthlySummary.FromPersistence(id, userId, month, year, totalIncome, totalExpense);

        private static void Seed(params MonthlySummary[] monthlySummaries)
            => MonthlySummarySingleton.Instance.AddRange(monthlySummaries);
    }
}
