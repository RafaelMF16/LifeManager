using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Text;
using LifeManager.Domain.Transactions.Enums;
using LifeManager.Infrastructure.Categories;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Transactions;

namespace LifeManager.Infrastructure.Test.Search
{
    /// <summary>
    /// The search runs on the persisted normalized column with an escaped LIKE (the trigram index's operator), so it
    /// ignores case and accents and treats % and _ as plain characters.
    /// </summary>
    public class SearchTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly PageRequest FirstPage = PageRequest.Create(1, 20).Unwrap();

        [Fact]
        public async Task CategorySearch_ShouldIgnoreCaseAndAccents_WhenTermDiffersFromStoredName()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            await context.AddCategoryAsync(user, "Mercadó");
            await context.AddCategoryAsync(user, "Lazer");

            var page = await new CategoryRepository(context)
                .GetPagedByUserIdAsync(user.Id!, FirstPage, SearchText.Normalize("MERCADO"), SortDirection.Asc, default);

            var found = Assert.Single(page.Items);
            Assert.Equal("Mercadó", found.Name.Value);
        }

        [Fact]
        public async Task CategorySearch_ShouldMatchWildcardsLiterally_WhenTermHasPercentOrUnderscore()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            await context.AddCategoryAsync(user, "50% off");
            await context.AddCategoryAsync(user, "50x off");
            await context.AddCategoryAsync(user, "a_b");
            await context.AddCategoryAsync(user, "axb");
            var repository = new CategoryRepository(context);

            var percent = await repository.GetPagedByUserIdAsync(user.Id!, FirstPage, SearchText.Normalize("50%"), SortDirection.Asc, default);
            var underscore = await repository.GetPagedByUserIdAsync(user.Id!, FirstPage, SearchText.Normalize("a_b"), SortDirection.Asc, default);

            Assert.Equal("50% off", Assert.Single(percent.Items).Name.Value);
            Assert.Equal("a_b", Assert.Single(underscore.Items).Name.Value);
        }

        [Fact]
        public async Task CategorySearch_ShouldReturnOnlyTheUsersCategories_WhenAnotherUserHasTheSameName()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var otherUser = await context.AddUserAsync("Bia");
            await context.AddCategoryAsync(user, "Mercado");
            await context.AddCategoryAsync(otherUser, "Mercado");

            var page = await new CategoryRepository(context)
                .GetPagedByUserIdAsync(user.Id!, FirstPage, SearchText.Normalize("mercado"), SortDirection.Asc, default);

            Assert.Equal(user.Id, Assert.Single(page.Items).UserId);
            Assert.Equal(1, page.TotalCount);
        }

        [Fact]
        public async Task TransactionSearch_ShouldIgnoreCaseAndAccentsAndEscapeWildcards_WhenSearchingTheMonth()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);
            var month = await context.AddMonthAsync(user, 2026, 11);
            var repository = new TransactionRepository(context);
            await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 10m, description: "Mercadó Central"), default);
            await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 20m, description: "Farmácia"), default);
            await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 30m, description: "Cupom 10% desconto"), default);
            await repository.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 40m, description: "Cupom 10x desconto"), default);

            var accents = await SearchAsync(repository, month.Id!, "MERCADO");
            var wildcard = await SearchAsync(repository, month.Id!, "10%");

            Assert.Equal("Mercadó Central", Assert.Single(accents.Items).Transaction.Description.Value);
            Assert.Equal("Cupom 10% desconto", Assert.Single(wildcard.Items).Transaction.Description.Value);
        }

        private static Task<PagedList<Domain.Transactions.TransactionListItem>> SearchAsync(
            TransactionRepository repository, Domain.MonthlySummaries.ValueObjects.MonthlySummaryId monthId, string term)
            => repository.GetPagedAsync(
                monthId, FirstPage, TransactionTypeFilter.All, categoryId: null, date: null, savingsGoalId: null,
                SearchText.Normalize(term), TransactionSortBy.Date, SortDirection.Desc, default);
    }
}
