using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Shared.DTOs
{
    /// <summary>Response envelope shared by every paged listing endpoint.</summary>
    public record PagedResponseDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize, int TotalPages)
    {
        public static PagedResponseDto<T> From<TSource>(PagedList<TSource> pagedList, Func<TSource, T> map)
        {
            var mapped = pagedList.Map(map);

            return new(mapped.Items, mapped.TotalCount, mapped.Page, mapped.PageSize, mapped.TotalPages);
        }
    }
}
