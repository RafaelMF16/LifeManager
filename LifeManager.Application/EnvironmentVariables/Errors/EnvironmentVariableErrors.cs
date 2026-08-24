using LifeManager.Domain.Shared.Results;

namespace LifeManager.Application.EnvironmentVariables.Errors
{
    public static class EnvironmentVariableErrors
    {
        public static Error KeyNotFound(string keyName)
            => Error.Failure("EnvironmentVariable.KeyNotFound", $"Environment variable [{keyName}] not found");
    }
}
