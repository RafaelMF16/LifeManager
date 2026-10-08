using LifeManager.Application.DI;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Categories.Mocks;
using LifeManager.Application.Test.MonthlySummaries.Mocks;
using LifeManager.Application.Test.Transactions.Mocks;
using LifeManager.Application.Test.FinanceDashboard.Mocks;
using LifeManager.Application.Test.RecurringTransactions.Mocks;
using LifeManager.Application.Test.Budgets.Mocks;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Domain.Budgets.Interfaces;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Test.UsersPreferences.Mocks;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.UsersPreferences.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Configurations
{
    public static class InjectionModule
    {
        public static IServiceCollection AddServicesInScope(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(configuration);

            services.AddApplicationServices();

            services.AddScoped<IUserRepository, UserRepositoryMock>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepositoryMock>();
            services.AddScoped<IUserPreferencesRepository, UserPreferencesRepositoryMock>();
            services.AddScoped<ICategoryRepository, CategoryRepositoryMock>();
            services.AddScoped<IMonthlySummaryRepository, MonthlySummaryRepositoryMock>();
            services.AddScoped<ITransactionRepository, TransactionRepositoryMock>();
            services.AddScoped<IFinanceDashboardRepository, FinanceDashboardRepositoryMock>();
            services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepositoryMock>();
            services.AddScoped<IBudgetRepository, BudgetRepositoryMock>();
            services.AddScoped<IPlayerProfileRepository, PlayerProfileRepositoryMock>();
            services.AddScoped<IHabitRepository, HabitRepositoryMock>();
            services.AddScoped<IHabitCheckInRepository, HabitCheckInRepositoryMock>();
            services.AddScoped<IHabitEvaluationRepository, HabitEvaluationRepositoryMock>();

            services.AddSingleton<TimeProvider, FakeTimeProvider>();

            return services;
        }
    }
}