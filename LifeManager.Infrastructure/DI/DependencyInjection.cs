using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Infrastructure.Categories;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Infrastructure.MonthlySummaries;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Infrastructure.Transactions;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.UsersPreferences.Interfaces;
using LifeManager.Infrastructure.Auth;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Users;
using LifeManager.Infrastructure.UsersPreferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Infrastructure.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<LifeManagerDbContext>(options => options.UseNpgsql(connectionString));

            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
            services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<IMonthlySummaryRepository, MonthlySummaryRepository>();
            services.AddScoped<ITransactionRepository, TransactionRepository>();

            return services;
        }
    }
}