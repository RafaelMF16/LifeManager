using LifeManager.Application.EnvironmentVariables.Services;
using LifeManager.Application.Test.Configurations;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.EnvironmentVariables
{
    [Collection("ApplicationServices")]
    public class EnvironmentVariableServiceTest : BaseTest
    {
        private readonly EnvironmentVariableService _environmentVariableService;

        public EnvironmentVariableServiceTest()
        {
            _environmentVariableService = ServiceProvider.GetRequiredService<EnvironmentVariableService>();
        }

        [Fact]
        public void GetEnvironmentVariable_ShouldReturnValue_WhenKeyExists()
        {
            var value = _environmentVariableService.GetEnvironmentVariable("lifeManagerTestOnlyKey");

            Assert.Equal("test-only-value-0123456789abcdef", value);
        }

        [Fact]
        public void GetEnvironmentVariable_ShouldThrow_WhenKeyDoesNotExist()
        {
            const string keyName = "missingKey";
            const string errorMessage = $"Environment variable [{keyName}] not found";

            var exception = Assert.Throws<Exception>(() => _environmentVariableService.GetEnvironmentVariable(keyName));
            Assert.Equal(errorMessage, exception.Message);
        }
    }
}
