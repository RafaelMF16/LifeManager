using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Shared.Paging
{
    public class PageRequest
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        public int Page { get; }
        public int PageSize { get; }
        public int Skip => (Page - 1) * PageSize;

        private PageRequest(int page, int pageSize)
        {
            Page = page;
            PageSize = pageSize;
        }

        public static Result<PageRequest> Create(int page, int pageSize)
        {
            if (page < 1)
                return PagingErrors.InvalidPage;

            if (pageSize < 1 || pageSize > MaxPageSize)
                return PagingErrors.InvalidPageSize;

            return new PageRequest(page, pageSize);
        }
    }
}
