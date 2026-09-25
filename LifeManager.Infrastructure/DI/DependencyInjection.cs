using LifeManager.Domain.Auth.Interfaces;
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

            return services;
        }
    }
}