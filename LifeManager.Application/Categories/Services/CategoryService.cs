using LifeManager.Application.Categories.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Categories.ValueObjects;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Categories.Services
{
    public class CategoryService(ICategoryRepository categoryRepository, ITransactionRepository transactionRepository)
    {
        private readonly ICategoryRepository _categoryRepository = categoryRepository;
        private readonly ITransactionRepository _transactionRepository = transactionRepository;

        public async Task<Result<CategoryResponseDto>> CreateAsync(CategoryDto categoryDto, UserId userId, CancellationToken cancellationToken)
        {
            var categoryResult = Category.Create(userId.Value, categoryDto.Name);
            if (!categoryResult.IsSuccess)
                return categoryResult.Error;

            var category = categoryResult.Value;

            if (await _categoryRepository.ExistsByNameAsync(userId, category.Name, null, cancellationToken))
                return CategoryErrors.NameAlreadyExists;

            await _categoryRepository.AddAsync(category, cancellationToken);

            return ToResponseDto(category);
        }

        public async Task<Result<CategoryResponseDto>> GetByIdAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(new CategoryId(id), userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            return ToResponseDto(category);
        }

        public async Task<Result<PagedResponseDto<CategoryResponseDto>>> GetPagedAsync(CategoryListQueryDto query, UserId userId, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error;

            var pageRequest = pageRequestResult.Value;
            var normalizedSearch = SearchText.Normalize(query.Search);

            if (normalizedSearch.Length > CategoryName.MaxLength)
                return new PagedResponseDto<CategoryResponseDto>([], 0, pageRequest.Page, pageRequest.PageSize, 0);

            var categories = await _categoryRepository.GetPagedByUserIdAsync(userId, pageRequest, normalizedSearch, query.SortDirection, cancellationToken);

            return PagedResponseDto<CategoryResponseDto>.From(categories, ToResponseDto);
        }

        public async Task<Result<CategoryResponseDto>> UpdateAsync(int id, CategoryDto categoryDto, UserId userId, CancellationToken cancellationToken)
        {
            var category = await _categoryRepository.GetByIdAsync(new CategoryId(id), userId, cancellationToken);
            if (category is null)
                return CategoryErrors.NotFound;

            var currentName = category.Name;

            var renameResult = category.Rename(categoryDto.Name);
            if (!renameResult.IsSuccess)
                return renameResult.Error;

            if (category.Name.Equals(currentName))
                return ToResponseDto(category);

            if (await _categoryRepository.ExistsByNameAsync(userId, category.Name, category.Id, cancellationToken))
                return CategoryErrors.NameAlreadyExists;

            await _categoryRepository.UpdateAsync(category, cancellationToken);

            return ToResponseDto(category);
        }

        public async Task<Result> DeleteAsync(int id, UserId userId, CancellationToken cancellationToken)
        {
            var categoryId = new CategoryId(id);

            if (await _transactionRepository.ExistsByCategoryAsync(categoryId, userId, cancellationToken))
                return CategoryErrors.InUse;

            var deleted = await _categoryRepository.DeleteAsync(categoryId, userId, cancellationToken);
            if (!deleted)
                return CategoryErrors.NotFound;

            return Result.Success();
        }

        private static CategoryResponseDto ToResponseDto(Category category)
            => new(category.Id!.Value, category.Name.Value);
    }
}
