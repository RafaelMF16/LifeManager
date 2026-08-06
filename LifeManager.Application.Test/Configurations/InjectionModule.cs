using LifeManager.Application.Auth.Services;
using LifeManager.Application.EnvironmentVariables.Services;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Configurations
{
    public static class InjectionModule
    {
        public static void AddServicesInScope(IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(configuration);

            services.AddScoped<UserService>();
            services.AddScoped<IUserRepository, UserRepositoryMock>();

            services.AddScoped<AuthService>();
            services.AddScoped<TokenService>();
            services.AddScoped<EnvironmentVariableService>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepositoryMock>();
        }
    }
}