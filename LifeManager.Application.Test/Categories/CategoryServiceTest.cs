using LifeManager.Application.Categories.DTOs;
using LifeManager.Application.Categories.Services;
using LifeManager.Application.Test.Categories.Mocks;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Domain.Categories.Errors;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Categories
{
    [Collection("ApplicationServices")]
    public class CategoryServiceTest : BaseTest
    {
        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);

        private readonly CategoryService _categoryService;
        private readonly CategoryRepositoryMock _categoryRepository;

        public CategoryServiceTest()
        {
            _categoryService = ServiceProvider.GetRequiredService<CategoryService>();
            _categoryRepository = (CategoryRepositoryMock)ServiceProvider.GetRequiredService<ICategoryRepository>();

            CategorySingleton.Instance.Clear();
        }

        [Fact]
        public async Task CreateAsync_ShouldAddCategory_WhenNameIsValid()
        {
            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Name);
            var category = Assert.Single(CategorySingleton.Instance);
            Assert.Equal(result.Value.Id, category.Id!.Value);
            Assert.Equal(FirstUserId, category.UserId);
            Assert.Equal("Food", category.Name.Value);
        }

        [Fact]
        public async Task CreateAsync_ShouldTrimName_WhenNameHasSurroundingWhiteSpace()
        {
            var result = await _categoryService.CreateAsync(new CategoryDto("  Food  "), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Name);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnConflict_WhenUserAlreadyHasCategoryWithSameName()
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            Assert.Single(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnConflict_WhenNameOnlyDiffersBySurroundingWhiteSpace()
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto(" Food "), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
        }

        [Theory]
        [InlineData("food")]
        [InlineData("FOOD")]
        public async Task CreateAsync_ShouldReturnConflict_WhenNameOnlyDiffersByCase(string name)
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto(name), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
            Assert.Single(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnConflict_WhenNameOnlyDiffersByAccents()
        {
            await CreateCategory("Saúde", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto("Saude"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRenameCategory_WhenNewNameOnlyChangesCase()
        {
            var createdCategory = await CreateCategory("food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task CreateAsync_ShouldAddCategory_WhenOtherUserHasCategoryWithSameName()
        {
            await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), SecondUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, CategorySingleton.Instance.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateAsync_ShouldReturnValidationError_WhenNameIsNullOrWhiteSpace(string? name)
        {
            var result = await _categoryService.CreateAsync(new CategoryDto(name!), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
            Assert.Empty(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnValidationError_WhenNameIsTooLong()
        {
            var result = await _categoryService.CreateAsync(new CategoryDto(new string('a', 51)), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameTooLong, result.Error);
            Assert.Empty(CategorySingleton.Instance);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCategory_WhenCategoryBelongsToUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.GetByIdAsync(createdCategory.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(createdCategory, result.Value);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenCategoryDoesNotExist()
        {
            var result = await _categoryService.GetByIdAsync(99, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_WhenCategoryBelongsToOtherUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.GetByIdAsync(createdCategory.Id, SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnEmptyPage_WhenUserHasNoCategories()
        {
            await CreateCategory("Food", SecondUserId);

            var result = await _categoryService.GetPagedAsync(new CategoryListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(0, result.Value.TotalCount);
            Assert.Equal(0, result.Value.TotalPages);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnOnlyUserCategories_WhenOtherUsersHaveCategories()
        {
            await CreateCategory("Food", FirstUserId);
            await CreateCategory("Health", SecondUserId);
            await CreateCategory("Salary", FirstUserId);

            var result = await _categoryService.GetPagedAsync(new CategoryListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.Equal(["Food", "Salary"], result.Value!.Items.Select(category => category.Name));
            Assert.Equal(2, result.Value.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldOrderByNameIgnoringCaseAndAccents_WhenSortingAscending()
        {
            await CreateCategory("salário", FirstUserId);
            await CreateCategory("Educação", FirstUserId);
            await CreateCategory("Saúde", FirstUserId);
            await CreateCategory("Alimentação", FirstUserId);

            var result = await _categoryService.GetPagedAsync(new CategoryListQueryDto(), FirstUserId, CancellationToken.None);

            Assert.Equal(["Alimentação", "Educação", "salário", "Saúde"], result.Value!.Items.Select(category => category.Name));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldOrderByNameDescending_WhenSortDirectionIsDesc()
        {
            await CreateCategory("Food", FirstUserId);
            await CreateCategory("Salary", FirstUserId);
            await CreateCategory("Health", FirstUserId);

            var query = new CategoryListQueryDto { SortDirection = SortDirection.Desc };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal(["Salary", "Health", "Food"], result.Value!.Items.Select(category => category.Name));
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnRequestedPage_WhenThereAreMoreCategoriesThanPageSize()
        {
            foreach (var name in new[] { "A", "B", "C", "D", "E" })
                await CreateCategory(name, FirstUserId);

            var query = new CategoryListQueryDto { Page = 2, PageSize = 2 };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(["C", "D"], result.Value.Items.Select(category => category.Name));
            Assert.Equal(5, result.Value.TotalCount);
            Assert.Equal(2, result.Value.Page);
            Assert.Equal(2, result.Value.PageSize);
            Assert.Equal(3, result.Value.TotalPages);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnEmptyItemsWithTotal_WhenPageIsBeyondLastPage()
        {
            await CreateCategory("Food", FirstUserId);

            var query = new CategoryListQueryDto { Page = 3, PageSize = 10 };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(1, result.Value.TotalCount);
            Assert.Equal(1, result.Value.TotalPages);
        }

        [Theory]
        [InlineData("saude")]
        [InlineData("SAÚDE")]
        [InlineData("aud")]
        [InlineData("  saú  ")]
        public async Task GetPagedAsync_ShouldMatchIgnoringCaseAndAccents_WhenSearchIsProvided(string search)
        {
            await CreateCategory("Saúde", FirstUserId);
            await CreateCategory("Salário", FirstUserId);

            var query = new CategoryListQueryDto { Search = search };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal(["Saúde"], result.Value!.Items.Select(category => category.Name));
            Assert.Equal(1, result.Value.TotalCount);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetPagedAsync_ShouldNotFilter_WhenSearchIsNullOrWhiteSpace(string? search)
        {
            await CreateCategory("Food", FirstUserId);
            await CreateCategory("Health", FirstUserId);

            var query = new CategoryListQueryDto { Search = search };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.Equal(2, result.Value!.TotalCount);
        }

        [Fact]
        public async Task GetPagedAsync_ShouldReturnEmptyPage_WhenSearchIsLongerThanAnyName()
        {
            await CreateCategory("Food", FirstUserId);

            var query = new CategoryListQueryDto { Search = new string('f', 51) };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(0, result.Value.TotalCount);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GetPagedAsync_ShouldReturnValidationError_WhenPageIsLessThanOne(int page)
        {
            var query = new CategoryListQueryDto { Page = page };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(PagingErrors.InvalidPage, result.Error);
            Assert.Equal(ErrorType.Validation, result.Error.Type);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(PageRequest.MaxPageSize + 1)]
        public async Task GetPagedAsync_ShouldReturnValidationError_WhenPageSizeIsOutOfRange(int pageSize)
        {
            var query = new CategoryListQueryDto { PageSize = pageSize };
            var result = await _categoryService.GetPagedAsync(query, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(PagingErrors.InvalidPageSize, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRenameCategory_WhenNameIsValid()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Groceries"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(new CategoryResponseDto(createdCategory.Id, "Groceries"), result.Value);
            var category = Assert.Single(CategorySingleton.Instance);
            Assert.Equal("Groceries", category.Name.Value);
        }

        [Theory]
        [InlineData("Food")]
        [InlineData("  Food  ")]
        public async Task UpdateAsync_ShouldSucceedWithoutWriting_WhenNameIsTheSameAsCurrentName(string name)
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);
            var existsByNameCallCountBeforeUpdate = _categoryRepository.ExistsByNameCallCount;

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto(name), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(createdCategory, result.Value);
            Assert.Equal(existsByNameCallCountBeforeUpdate, _categoryRepository.ExistsByNameCallCount);
            Assert.Equal(0, _categoryRepository.UpdateCallCount);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldWrite_WhenNameChanged()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Groceries"), FirstUserId, CancellationToken.None);

            Assert.Equal(1, _categoryRepository.UpdateCallCount);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnConflict_WhenUserHasOtherCategoryWithSameName()
        {
            await CreateCategory("Food", FirstUserId);
            var categoryToRename = await CreateCategory("Health", FirstUserId);

            var result = await _categoryService.UpdateAsync(categoryToRename.Id, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameAlreadyExists, result.Error);
            Assert.Equal("Health", CategorySingleton.Instance.Single(category => category.Id!.Value == categoryToRename.Id).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldRenameCategory_WhenOtherUserHasCategoryWithSameName()
        {
            await CreateCategory("Food", SecondUserId);
            var createdCategory = await CreateCategory("Health", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", result.Value.Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task UpdateAsync_ShouldKeepOriginalName_WhenNameIsNullOrWhiteSpace(string? name)
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto(name!), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NameIsNullOrWhiteSpace, result.Error);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFound_WhenCategoryDoesNotExist()
        {
            var result = await _categoryService.UpdateAsync(99, new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnNotFoundAndKeepName_WhenCategoryBelongsToOtherUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.UpdateAsync(createdCategory.Id, new CategoryDto("Groceries"), SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveCategory_WhenCategoryBelongsToUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);
            var remainingCategory = await CreateCategory("Health", FirstUserId);

            var result = await _categoryService.DeleteAsync(createdCategory.Id, FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(remainingCategory.Id, Assert.Single(CategorySingleton.Instance).Id!.Value);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFound_WhenCategoryDoesNotExist()
        {
            var result = await _categoryService.DeleteAsync(99, FirstUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Equal(ErrorType.NotFound, result.Error.Type);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnNotFoundAndKeepCategory_WhenCategoryBelongsToOtherUser()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);

            var result = await _categoryService.DeleteAsync(createdCategory.Id, SecondUserId, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(CategoryErrors.NotFound, result.Error);
            Assert.Single(CategorySingleton.Instance);
        }

        [Fact]
        public async Task CreateAsync_ShouldAllowSameName_WhenPreviousCategoryWasDeleted()
        {
            var createdCategory = await CreateCategory("Food", FirstUserId);
            await _categoryService.DeleteAsync(createdCategory.Id, FirstUserId, CancellationToken.None);

            var result = await _categoryService.CreateAsync(new CategoryDto("Food"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("Food", Assert.Single(CategorySingleton.Instance).Name.Value);
        }

        private async Task<CategoryResponseDto> CreateCategory(string name, UserId userId)
        {
            var result = await _categoryService.CreateAsync(new CategoryDto(name), userId, CancellationToken.None);

            return result.Value!;
        }
    }
}
