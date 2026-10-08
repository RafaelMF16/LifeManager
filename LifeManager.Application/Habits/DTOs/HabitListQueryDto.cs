using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;

namespace LifeManager.Application.Habits.DTOs
{
    /// <summary>Query-string parameters of <c>GET /api/Habits</c>.</summary>
    public record HabitListQueryDto
    {
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = PageRequest.DefaultPageSize;
        public HabitStatusFilter Status { get; init; } = HabitStatusFilter.Active;
        public string? Search { get; init; }
        public HabitSortBy SortBy { get; init; } = HabitSortBy.Name;
        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
