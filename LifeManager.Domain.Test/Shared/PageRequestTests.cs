using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Domain.Test.Shared
{
    public class PageRequestTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_ShouldReturnFailure_WhenPageIsLessThanOne(int page)
        {
            var result = PageRequest.Create(page, 10);

            Assert.False(result.IsSuccess);
            Assert.Equal(PagingErrors.InvalidPage, result.Error);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(PageRequest.MaxPageSize + 1)]
        public void Create_ShouldReturnFailure_WhenPageSizeIsOutOfRange(int pageSize)
        {
            var result = PageRequest.Create(1, pageSize);

            Assert.False(result.IsSuccess);
            Assert.Equal(PagingErrors.InvalidPageSize, result.Error);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(PageRequest.MaxPageSize)]
        public void Create_ShouldReturnPageRequest_WhenPageSizeIsWithinRange(int pageSize)
        {
            var result = PageRequest.Create(1, pageSize);

            Assert.True(result.IsSuccess);
            Assert.Equal(pageSize, result.Value.PageSize);
        }

        [Theory]
        [InlineData(1, 20, 0)]
        [InlineData(3, 20, 40)]
        public void Skip_ShouldBeOffsetOfPreviousPages(int page, int pageSize, int expectedSkip)
        {
            var pageRequest = PageRequest.Create(page, pageSize).Value!;

            Assert.Equal(expectedSkip, pageRequest.Skip);
        }

        [Theory]
        [InlineData(0, 10, 0)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        public void TotalPages_ShouldRoundUp(int totalCount, int pageSize, int expectedTotalPages)
        {
            var pagedList = new PagedList<int>([], totalCount, 1, pageSize);

            Assert.Equal(expectedTotalPages, pagedList.TotalPages);
        }
    }
}
