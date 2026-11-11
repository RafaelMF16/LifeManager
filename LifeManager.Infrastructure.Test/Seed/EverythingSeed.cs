using LifeManager.Domain.AccountHolders;
using LifeManager.Domain.Auth;
using LifeManager.Domain.BudgetPlans;
using LifeManager.Domain.Categorization;
using LifeManager.Domain.Categorization.Enums;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.RecurringTransactions;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.StatementImports;
using LifeManager.Domain.StatementImports.Enums;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users;
using LifeManager.Domain.UsersPreferences;
using LifeManager.Domain.UsersPreferences.Enums;
using LifeManager.Infrastructure.AccountHolders;
using LifeManager.Infrastructure.BudgetPlans;
using LifeManager.Infrastructure.Habits;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.RecurringTransactions;
using LifeManager.Infrastructure.StatementImports;
using LifeManager.Infrastructure.Transactions;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Seed
{
    /// <summary>One user with data in every module, for the tests that must cover the whole schema (deletion, export).</summary>
    public static class EverythingSeed
    {
        private static readonly DateOnly Today = new(2026, 11, 10);

        /// <summary>A user with data in every module: finance, savings goals, recurrences, budget, imports and rules, habits and the game, rewards, account holders, auth.</summary>
        public static async Task<User> AddUserWithEverythingAsync(this LifeManagerDbContext context, string name)
        {
            var user = await context.AddUserAsync(name);
            var userId = user.Id!.Value;
            var category = await context.AddCategoryAsync(user, "Mercado");
            var month = await context.AddMonthAsync(user, 2026, 11);

            context.Add(UserPreferences.Create(userId, Theme.Dark, Language.PTBR).Unwrap());
            context.Add(RefreshToken.Create(userId, $"hash-{name}", TestData.Now, TestData.Now.AddDays(7), TestData.Now.AddDays(30), false).Unwrap());
            context.Add(Reward.Create(userId, "Cinema", 50, null, TestData.Now, category.Id!.Value, 40m).Unwrap());
            await context.SaveChangesAsync();

            var transactions = new TransactionRepository(context);
            await transactions.AddAsync(TestData.NewTransaction(month, category, MoneyFlowType.Expense, 25m), default);

            // A savings goal with a linked investment: deleting the user takes both, through two cascades and a SET NULL.
            var goal = await context.AddSavingsGoalAsync(user, "Viagem");
            await transactions.AddAsync(
                Transaction.Create(MoneyFlowType.Investment, category.Id!.Value, 300m, "Aporte", new DateOnly(2026, 11, 5), month, goal.Id!.Value).Unwrap(),
                default);

            var recurrence = RecurringTransaction
                .Create(userId, MoneyFlowType.Expense, category.Id!.Value, 100m, "Academia", 1, "2026-11", null, Today).Unwrap();
            var recurrenceRepository = new RecurringTransactionRepository(context);
            await recurrenceRepository.AddAsync(recurrence, default);
            var posted = Transaction.CreateFromRecurrence(recurrence, month).Unwrap();
            var expectedNextMonth = recurrence.NextMonth;
            recurrence.AdvanceAfterPosting();
            Assert.True(await recurrenceRepository.TryPostOccurrenceAsync(recurrence, expectedNextMonth, posted, default));

            var slices = new[] { BudgetSlice.Create(MoneyFlowType.Expense, category.Id!.Value, 50m).Unwrap() };
            await new BudgetPlanRepository(context)
                .SaveAsync(BudgetPlan.Create(userId, TestData.Month(2026, 11), 2000m, slices, TestData.Now).Unwrap(), default);

            await new AccountHolderRepository(context)
                .ReplaceAsync(AccountHolderList.Create(user.Id!, [$"{name} da Silva"]).Unwrap(), default);

            await SeedImportsAsync(context, user, category, month);
            await SeedHabitAsync(context, user);

            return user;
        }

        /// <summary>A committed import (its transaction and a learned rule) and a draft still holding its rows.</summary>
        private static async Task SeedImportsAsync(LifeManagerDbContext context, User user, Domain.Categories.Category category, MonthlySummary month)
        {
            var repository = new StatementImportRepository(context);

            var committedRow = NewRow(category, "A");
            var committed = StatementImport.Create(user.Id!.Value, "out.ofx", StatementImportSource.Ofx, [committedRow], TestData.Now).Unwrap();
            await repository.AddAsync(committed, [committedRow], default);
            RuleLearning[] learnings = [new("padaria", MoneyFlowType.Expense, RuleAction.Categorize, category.Id, 1)];
            await repository.CommitAsync(committed, [Transaction.CreateFromImport(committedRow, month).Unwrap()], learnings, 0, TestData.Now, Today, default);

            var draftRow = NewRow(category, "B");
            var draft = StatementImport.Create(user.Id!.Value, "nov.ofx", StatementImportSource.Ofx, [draftRow], TestData.Now).Unwrap();
            await repository.AddAsync(draft, [draftRow], default);
        }

        private static StatementImportRow NewRow(Domain.Categories.Category category, string fitid)
            => StatementImportRow.Create(new StatementLine(new DateOnly(2026, 11, 4), "Padaria", -8m, fitid), "conta", Today).Unwrap()
                .EditInBulk(null, category.Id!.Value, null).Unwrap();

        /// <summary>A habit checked in today: the check-in, the player's profile and a ledger entry.</summary>
        private static async Task SeedHabitAsync(LifeManagerDbContext context, User user)
        {
            var habit = await context.AddHabitAsync(user, "Ler", Today.AddDays(-1));

            await new HabitCheckInRepository(context).RecordAsync(habit, Today, HabitCheckInStatus.Done, TestData.Now, checkIn =>
            {
                var outcome = checkIn.Profile.Apply(new GameDelta(10, 5, 0));
                var entries = GameLedgerEntry.FromOutcome(habit.UserId, GameLedgerEntryKind.HabitDone, Today, TestData.Now, habit.Name.Value, outcome, habit.Id);

                return new HabitCheckInEffects(1, outcome.Applied, outcome, entries);
            }, default);
        }
    }
}
