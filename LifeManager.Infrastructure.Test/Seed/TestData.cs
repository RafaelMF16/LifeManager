using LifeManager.Domain.Push;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.SavingsGoals;
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

        /// <summary>An active savings goal without a deadline (the deadline rule depends on the clock).</summary>
        public static async Task<SavingsGoal> AddSavingsGoalAsync(this LifeManagerDbContext context, User user, string name = "Viagem", decimal target = 5000m)
        {
            var goal = SavingsGoal.Create(user.Id!.Value, name, target, null, 0m, null, Month(2026, 11), Now).Unwrap();
            context.Add(goal);
            await context.SaveChangesAsync();

            return goal;
        }

        /// <summary>A transaction built by the domain, not stored: the test hands it to the repository under test.</summary>
        /// <summary>A browser's subscription keys: 65 and 16 bytes, base64url.</summary>
        public static readonly string PushP256dh = Convert.ToBase64String(Enumerable.Repeat((byte)4, 65).ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public static readonly string PushAuth = Convert.ToBase64String(Enumerable.Repeat((byte)7, 16).ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static PushSubscription NewPushSubscription(User user, string endpoint, DateTimeOffset? createdAt = null, string? label = null)
            => PushSubscription.Create(user.Id!.Value, endpoint, PushP256dh, PushAuth, label, createdAt ?? Now).Unwrap();

        public static Transaction NewTransaction(
            MonthlySummary month, Category category, MoneyFlowType type, decimal amount, int day = 1, string description = "Compra")
        {
            var date = new DateOnly(month.Year.Value, month.Month.Value, day);

            return Transaction.Create(type, category.Id!.Value, amount, description, date, month).Unwrap();
        }

        /// <summary>A daily habit to build, started on <paramref name="startDate"/>.</summary>
        public static async Task<Habit> AddHabitAsync(
            this LifeManagerDbContext context, User user, string name, DateOnly startDate, HabitKind kind = HabitKind.Positive, int? reminderHour = null)
        {
            var habit = Habit.Create(
                user.Id!.Value, name, description: null, trigger: null, kind, HabitDifficulty.Easy,
                HabitFrequencyType.Daily, HabitWeekDays.None, timesPerWeek: null, startDate, Now, reminderHour: reminderHour).Unwrap();
            context.Add(habit);
            await context.SaveChangesAsync();

            return habit;
        }
    }
}
