namespace LifeManager.Domain.Shared.Paging
{
    public record PagedList<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
    {
        public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public PagedList<TOut> Map<TOut>(Func<T, TOut> map)
            => new([.. Items.Select(map)], TotalCount, Page, PageSize);
    }
}
