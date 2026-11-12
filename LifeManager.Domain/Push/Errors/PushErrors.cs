using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Push.Errors
{
    public static class PushErrors
    {
        public static readonly Error NotConfigured = Error.NotFound("Push.NotConfigured", "Push notifications are not configured on this server");
    }
}
