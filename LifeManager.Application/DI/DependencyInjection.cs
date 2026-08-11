using LifeManager.Application.Auth.Services;
using LifeManager.Application.EnvironmentVariables.Services;
using LifeManager.Application.Users.Services;
using Microsoft.Extensions.DependencyInjection;

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
            return services;
        }
    }
}