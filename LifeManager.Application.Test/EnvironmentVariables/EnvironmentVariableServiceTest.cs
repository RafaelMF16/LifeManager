using LifeManager.Application.EnvironmentVariables.Errors;
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
            var result = _environmentVariableService.GetEnvironmentVariable("lifeManagerTestOnlyKey");

            Assert.True(result.IsSuccess);
            Assert.Equal("test-only-value-0123456789abcdef", result.Value);
        }

        [Fact]
        public void GetEnvironmentVariable_ShouldReturnFailure_WhenKeyDoesNotExist()
        {
            const string keyName = "missingKey";

            var result = _environmentVariableService.GetEnvironmentVariable(keyName);

            Assert.False(result.IsSuccess);
            Assert.Equal(EnvironmentVariableErrors.KeyNotFound(keyName), result.Error);
        }
    }
}
