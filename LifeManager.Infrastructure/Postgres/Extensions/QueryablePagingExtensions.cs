using LifeManager.Domain.Shared.Paging;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Postgres.Extensions
{
    public static class QueryablePagingExtensions
    {
        /// <summary>
        /// Runs the COUNT and the page query (OFFSET/LIMIT) for an already filtered and ordered query.
        /// The ordering must be deterministic (end with a unique key such as Id) for pages to be stable.
        /// </summary>
        public static async Task<PagedList<T>> ToPagedListAsync<T>(this IQueryable<T> query, PageRequest pageRequest, CancellationToken cancellationToken)
        {
            var totalCount = await query.CountAsync(cancellationToken);

            var items = totalCount == 0
                ? []
                : await query.Skip(pageRequest.Skip).Take(pageRequest.PageSize).ToListAsync(cancellationToken);

            return new PagedList<T>(items, totalCount, pageRequest.Page, pageRequest.PageSize);
        }

        public const string LikeEscapeCharacter = @"\";

        /// <summary>
        /// Builds a "contains" LIKE pattern with the user's wildcards escaped, so input like "50%" is matched
        /// literally. Pass <see cref="LikeEscapeCharacter"/> as the escape character to <c>EF.Functions.Like</c>.
        /// </summary>
        public static string ToContainsLikePattern(string value)
            => $"%{value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_")}%";
    }
}
