using LifeManager.Application.DI;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Categories.Mocks;
using LifeManager.Application.Test.MonthlySummaries.Mocks;
using LifeManager.Application.Test.Transactions.Mocks;
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

            return services;
        }
    }
}