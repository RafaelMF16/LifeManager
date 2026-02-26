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