using LifeManager.Application.EnvironmentVariables.Errors;
using LifeManager.Domain.Shared.Results;
using Microsoft.Extensions.Configuration;

namespace LifeManager.Application.EnvironmentVariables.Services
{
    public class EnvironmentVariableService(IConfiguration configuration)
    {
        private readonly IConfiguration _configuration = configuration;

        public Result<string> GetEnvironmentVariable(string keyName)
        {
            var value = _configuration[keyName];

            if (string.IsNullOrEmpty(value))
                return EnvironmentVariableErrors.KeyNotFound(keyName);

            return value;
        }
    }
}
