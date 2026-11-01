using LifeManager.Application.Test.Configurations.SingletonLists;
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
                    ["refreshTokenSecretKey"] = "test-refresh-token-secret-key-0123456789abcdef",
                    ["lifeManagerTestOnlyKey"] = "test-only-value-0123456789abcdef"
                })
                .AddEnvironmentVariables().Build();

            var services = new ServiceCollection();
            services.AddServicesInScope(configuration);
            ServiceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            // Every profile read settles the active challenges: none may leak into the next test and pay there.
            WeeklyChallengeSingleton.Instance.Clear();
        }
    }
}