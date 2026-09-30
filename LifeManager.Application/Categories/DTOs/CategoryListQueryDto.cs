using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Categories.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Categories</c>.</summary>
    public record CategoryListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public string? Search { get; init; }
        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
