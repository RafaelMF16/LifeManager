using LifeManager.Application.Auth.Services;
using LifeManager.Application.Budgets.Services;
using LifeManager.Application.Categories.Services;
using LifeManager.Application.FinanceDashboard.Services;
using LifeManager.Application.Habits.Services;
using LifeManager.Application.MonthlySummaries.Services;
using LifeManager.Application.RecurringTransactions.Services;
using LifeManager.Application.Shared.Time;
using LifeManager.Application.Transactions.Services;
using LifeManager.Application.EnvironmentVariables.Services;
using LifeManager.Application.Users.Services;
using LifeManager.Application.UsersPreferences.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LifeManager.Application.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<AuthService>();
            services.AddScoped<TokenService>();
            services.AddScoped<UserService>();
            services.AddScoped<EnvironmentVariableService>();
            services.AddScoped<UserPreferencesService>();
            services.AddScoped<CategoryService>();
            services.AddScoped<MonthlySummaryService>();
            services.AddScoped<TransactionService>();
            services.AddScoped<FinanceDashboardService>();
            services.AddScoped<RecurringTransactionService>();
            services.AddScoped<RecurringTransactionPostingService>();
            services.AddScoped<BudgetService>();
            services.AddScoped<PlayerWalletService>();
            services.AddScoped<HabitService>();

            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<AppClock>();
            return services;
        }
    }
}