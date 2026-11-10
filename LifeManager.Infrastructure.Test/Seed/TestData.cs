using LifeManager.Domain.Categories;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Shared.Enums;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Shared.ValueObjects;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users;
using LifeManager.Infrastructure.Postgres;

namespace LifeManager.Infrastructure.Test.Seed
{
    /// <summary>
    /// Builders that store data through the DbContext with the domain's own factories, so a seed is always a state
    /// the application could have produced. Each one saves right away and returns the stored entity, id included.
    /// </summary>
    public static class TestData
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);

        public static YearMonth Month(int year, int month) => YearMonth.From(new DateOnly(year, month, 1));

        public static T Unwrap<T>(this Result<T> result)
        {
            if (!result.IsSuccess)
                throw new InvalidOperationException($"Invalid seed: {result.Error?.Code} {result.Error?.Message}");

            return result.Value;
        }

        public static async Task<User> AddUserAsync(this LifeManagerDbContext context, string name = "Ana", string? email = null)
        {
            var user = User.Create(name, email ?? $"{Guid.NewGuid():N}@lifemanager.test", "$2a$05$seedhashseedhashseedhashse", Now).Unwrap();
            context.Add(user);
            await context.SaveChangesAsync();

            return user;
        }

        public static async Task<Category> AddCategoryAsync(this LifeManagerDbContext context, User user, string name = "Mercado")
        {
            var category = Category.Create(user.Id!.Value, name).Unwrap();
            context.Add(category);
            await context.SaveChangesAsync();

            return category;
        }

        /// <summary>A month of any year (stored months may be from past years, like the ones imports open).</summary>
        public static async Task<MonthlySummary> AddMonthAsync(this LifeManagerDbContext context, User user, int year, int month)
        {
            var monthlySummary = MonthlySummary.OpenForImport(user.Id!, Month(year, month));
            context.Add(monthlySummary);
            await context.SaveChangesAsync();

            return monthlySummary;
        }

        /// <summary>A transaction built by the domain, not stored: the test hands it to the repository under test.</summary>
        public static Transaction NewTransaction(
            MonthlySummary month, Category category, MoneyFlowType type, decimal amount, int day = 1, string description = "Compra")
        {
            var date = new DateOnly(month.Year.Value, month.Month.Value, day);

            return Transaction.Create(type, category.Id!.Value, amount, description, date, month).Unwrap();
        }

        /// <summary>A daily habit to build, started on <paramref name="startDate"/>.</summary>
        public static async Task<Habit> AddHabitAsync(this LifeManagerDbContext context, User user, string name, DateOnly startDate)
        {
            var habit = Habit.Create(
                user.Id!.Value, name, description: null, trigger: null, HabitKind.Positive, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, timesPerWeek: null, startDate, Now).Unwrap();
            context.Add(habit);
            await context.SaveChangesAsync();

            return habit;
        }
    }
}
