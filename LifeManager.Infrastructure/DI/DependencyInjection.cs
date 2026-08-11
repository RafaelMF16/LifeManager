using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Infrastructure.Auth;
using LifeManager.Infrastructure.Users;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Infrastructure.DI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

            return services;
        }
    }
}