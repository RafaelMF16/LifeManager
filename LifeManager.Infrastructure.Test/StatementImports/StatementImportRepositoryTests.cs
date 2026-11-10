using LifeManager.Domain.Categories;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users;
using LifeManager.Infrastructure.MonthlySummaries;
using LifeManager.Infrastructure.StatementImports;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.StatementImports
{
    public class StatementImportRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly Today = new(2026, 11, 10);
        private const string AccountKey = "conta-teste";

        [Fact]
        public async Task CommitAsync_ShouldPostIntoOpenedMonthsAndSkipKnownLines_WhenDraftSpansTwoMonths()
        {
            var (user, category) = await SeedUserAsync();
            await CommitNewImportAsync(user, category, [new StatementLine(new DateOnly(2026, 11, 3), "Padaria Pão Bom", -12.50m, "A")]);

            var outcome = await CommitNewImportAsync(user, category,
            [
                new StatementLine(new DateOnly(2026, 11, 3), "Padaria Pão Bom", -12.50m, "A"), // already imported
                new StatementLine(new DateOnly(2026, 11, 5), "Salário", 3000m, "B"),
                new StatementLine(new DateOnly(2026, 10, 20), "Farmácia", -40m, "C"),             // month not opened yet
            ]);

            Assert.Equal(new StatementImportCommitOutcome(2, 1), outcome);

            await using var context = Fixture.CreateContext();
            var months = await context.MonthlySummaries.AsNoTracking().OrderBy(month => month.Month).ToListAsync();
            Assert.Collection(months,
                october =>
                {
                    Assert.Equal(10, october.Month.Value);
                    Assert.Equal(40m, october.TotalExpense.Value);
                },
                november =>
                {
                    Assert.Equal(12.50m, november.TotalExpense.Value);
                    Assert.Equal(3000m, november.TotalIncome.Value);
                    Assert.Equal(2987.50m, november.BalanceAmount);
                });
            Assert.Equal(3, await context.Transactions.CountAsync());
            Assert.Equal(0, await context.StatementImportRows.CountAsync());
        }

        [Fact]
        public async Task CommitAsync_ShouldAddHitsToExistingRules_WhenTheSameLearningsAreCommittedTwice()
        {
            var (user, category) = await SeedUserAsync();
            // A category rule and a month rule (no category: NULLS NOT DISTINCT must still find it on the second upsert).
            IReadOnlyList<RuleLearning> learnings =
            [
                new RuleLearning("padaria pao bom", MoneyFlowType.Expense, RuleAction.Categorize, category.Id, 2),
                new RuleLearning("salario", MoneyFlowType.Income, RuleAction.NextMonth, null, 1),
            ];

            await CommitNewImportAsync(user, category, [new StatementLine(new DateOnly(2026, 11, 3), "Padaria", -1m, "A")], learnings);
            await CommitNewImportAsync(user, category, [new StatementLine(new DateOnly(2026, 11, 4), "Padaria", -1m, "B")], learnings);

            await using var context = Fixture.CreateContext();
            var rules = await context.CategorizationRules.AsNoTracking().OrderBy(rule => rule.MerchantKey).ToListAsync();
            Assert.Collection(rules,
                bakery =>
                {
                    Assert.Equal(RuleAction.Categorize, bakery.Action);
                    Assert.Equal(category.Id, bakery.CategoryId);
                    Assert.Equal(4, bakery.Hits);
                },
                salary =>
                {
                    Assert.Equal(RuleAction.NextMonth, salary.Action);
                    Assert.Null(salary.CategoryId);
                    Assert.Equal(2, salary.Hits);
                });
        }

        [Fact]
        public async Task CommitAsync_ShouldReturnNullAndWriteNothing_WhenTheDraftWasAlreadyCommitted()
        {
            var (user, category) = await SeedUserAsync();
            var line = new StatementLine(new DateOnly(2026, 11, 3), "Padaria", -10m, "A");
            var (statementImport, transactions) = await PrepareImportAsync(user, category, [line]);

            await using (var context = Fixture.CreateContext())
                Assert.NotNull(await Commit(context, statementImport, transactions, []));

            // The service builds new transactions for the second confirmation; the draft lock turns it away.
            var (_, retried) = await BuildTransactionsAsync(user, category, statementImport, [line]);
            StatementImportCommitOutcome? second;
            await using (var context = Fixture.CreateContext())
                second = await Commit(context, statementImport, retried, []);

            Assert.Null(second);
            await using var readContext = Fixture.CreateContext();
            Assert.Equal(1, await readContext.Transactions.CountAsync());
            var stored = await readContext.StatementImports.AsNoTracking().SingleAsync();
            Assert.Equal(StatementImportStatus.Committed, stored.Status);
            Assert.Equal(1, stored.ImportedCount);
        }

        private async Task<(User User, Category Category)> SeedUserAsync()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var category = await context.AddCategoryAsync(user);

            return (user, category);
        }

        private async Task<StatementImportCommitOutcome?> CommitNewImportAsync(
            User user, Category category, IReadOnlyList<StatementLine> lines, IReadOnlyList<RuleLearning>? learnings = null)
        {
            var (statementImport, transactions) = await PrepareImportAsync(user, category, lines);

            await using var context = Fixture.CreateContext();
            return await Commit(context, statementImport, transactions, learnings ?? []);
        }

        /// <summary>Stores the draft (every row filed into the category), then opens months and builds transactions like the service.</summary>
        private async Task<(StatementImport Import, IReadOnlyList<Transaction> Transactions)> PrepareImportAsync(
            User user, Category category, IReadOnlyList<StatementLine> lines)
        {
            var rows = lines
                .Select(line => StatementImportRow.Create(line, AccountKey, Today).Unwrap().EditInBulk(null, category.Id!.Value, null).Unwrap())
                .ToList();
            var statementImport = StatementImport.Create(user.Id!.Value, "extrato.ofx", StatementImportSource.Ofx, rows, TestData.Now).Unwrap();

            await using (var context = Fixture.CreateContext())
                await new StatementImportRepository(context).AddAsync(statementImport, rows, default);

            return await BuildTransactionsAsync(user, category, statementImport, lines, rows);
        }

        private async Task<(StatementImport Import, IReadOnlyList<Transaction> Transactions)> BuildTransactionsAsync(
            User user, Category category, StatementImport statementImport, IReadOnlyList<StatementLine> lines, IReadOnlyList<StatementImportRow>? rows = null)
        {
            rows ??= lines
                .Select(line =>
                {
                    var row = StatementImportRow.Create(line, AccountKey, Today).Unwrap().EditInBulk(null, category.Id!.Value, null).Unwrap();
                    row.AssignImport(statementImport.Id!);
                    return row;
                })
                .ToList();

            await using var context = Fixture.CreateContext();
            var monthRepository = new MonthlySummaryRepository(context);
            var transactions = new List<Transaction>();
            foreach (var row in rows)
            {
                var month = await monthRepository.AddIfMissingAsync(MonthlySummary.OpenForImport(user.Id!, YearMonth.From(row.EffectiveDate)), default);
                transactions.Add(Transaction.CreateFromImport(row, month).Unwrap());
            }

            return (statementImport, transactions);
        }

        private static Task<StatementImportCommitOutcome?> Commit(
            Postgres.LifeManagerDbContext context, StatementImport statementImport, IReadOnlyList<Transaction> transactions, IReadOnlyList<RuleLearning> learnings)
            => new StatementImportRepository(context).CommitAsync(statementImport, transactions, learnings, createdMonthCount: 0, TestData.Now, Today, default);
    }
}
