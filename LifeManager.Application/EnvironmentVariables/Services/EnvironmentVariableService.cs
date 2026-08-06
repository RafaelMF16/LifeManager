using Microsoft.Extensions.Configuration;

namespace LifeManager.Application.EnvironmentVariables.Services
{
    public class EnvironmentVariableService(IConfiguration configuration)
    {
        private readonly IConfiguration _configuration = configuration;

        public string GetEnvironmentVariable(string keyName)
        {
            return _configuration[keyName]
                ?? throw new Exception($"Environment variable [{keyName}] not found");
        }
    }
}