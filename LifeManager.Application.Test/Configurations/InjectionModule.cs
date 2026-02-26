using LifeManager.Application.Auth;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users;
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
        }
    }
}