using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Test.Categories.Mocks
{
    public class CategoryRepositoryMock : ICategoryRepository
    {
        private readonly CategorySingleton _instance;

        public int ExistsByNameCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }

        public CategoryRepositoryMock()
        {
            _instance = CategorySingleton.Instance;
        }

        public Task<Category> AddAsync(Category category, CancellationToken cancellationToken)
        {
            var newId = _instance.Count == 0 ? 1 : _instance.Max(storedCategory => storedCategory.Id!.Value) + 1;
            category.AssignId(newId);

            _instance.Add(category);

            return Task.FromResult(category);
        }

        public Task<Category?> GetByIdAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            var storedCategory = _instance.SingleOrDefault(category => category.Id == categoryId && category.UserId == userId);

            return Task.FromResult(storedCategory is null ? null : ToDetachedCopy(storedCategory));
        }

        public Task<PagedList<Category>> GetPagedByUserIdAsync(UserId userId, PageRequest pageRequest, string? normalizedSearch, SortDirection sortDirection, CancellationToken cancellationToken)
        {
            var query = _instance.Where(category => category.UserId == userId);

            if (!string.IsNullOrEmpty(normalizedSearch))
                query = query.Where(category => category.NormalizedName.Contains(normalizedSearch, StringComparison.Ordinal));

            var ordered = sortDirection == SortDirection.Desc
                ? query.OrderByDescending(category => category.NormalizedName, StringComparer.Ordinal).ThenByDescending(category => category.Id!.Value)
                : query.OrderBy(category => category.NormalizedName, StringComparer.Ordinal).ThenBy(category => category.Id!.Value);

            var matching = ordered.ToList();
            IReadOnlyList<Category> items = [.. matching.Skip(pageRequest.Skip).Take(pageRequest.PageSize).Select(ToDetachedCopy)];

            return Task.FromResult(new PagedList<Category>(items, matching.Count, pageRequest.Page, pageRequest.PageSize));
        }

        public Task<bool> ExistsByNameAsync(UserId userId, CategoryName name, CategoryId? ignoredCategoryId, CancellationToken cancellationToken)
        {
            ExistsByNameCallCount++;

            var exists = _instance.Any(category =>
                category.UserId == userId
                && category.NormalizedName == name.NormalizedValue
                && (ignoredCategoryId is null || category.Id != ignoredCategoryId));

            return Task.FromResult(exists);
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken)
        {
            UpdateCallCount++;

            var index = _instance.FindIndex(storedCategory => storedCategory.Id == category.Id && storedCategory.UserId == category.UserId);
            if (index >= 0)
                _instance[index] = ToDetachedCopy(category);

            return Task.CompletedTask;
        }

        public Task<bool> DeleteAsync(CategoryId categoryId, UserId userId, CancellationToken cancellationToken)
        {
            var deletedRows = _instance.RemoveAll(category => category.Id == categoryId && category.UserId == userId);

            return Task.FromResult(deletedRows > 0);
        }

        private static Category ToDetachedCopy(Category category)
        {
            var copy = Category.Create(category.UserId.Value, category.Name.Value).Value!;
            copy.AssignId(category.Id!.Value);

            return copy;
        }
    }
}
