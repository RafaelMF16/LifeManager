using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Shared.Paging
{
    public static class PagingErrors
    {
        public static readonly Error InvalidPage = Error.Validation("Paging.InvalidPage", "Page must be greater than or equal to 1");
        public static readonly Error InvalidPageSize = Error.Validation("Paging.InvalidPageSize", $"PageSize must be between 1 and {PageRequest.MaxPageSize}");
    }
}
