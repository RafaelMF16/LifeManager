using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Configurations
{
    public class BaseTest : IDisposable
    {
        protected IServiceProvider ServiceProvider;

        public BaseTest()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["accessTokenSecretKey"] = "test-access-token-secret-key-0123456789abcdef",
                    ["refreshTokenSecretKey"] = "test-refresh-token-secret-key-0123456789abcdef"
                })
                .AddEnvironmentVariables().Build();

            var services = new ServiceCollection();
            InjectionModule.AddServicesInScope(services, configuration);
            ServiceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            
        }
    }
}