using LifeManager.Application.DI;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.Interfaces;
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

            return services;
        }
    }
}