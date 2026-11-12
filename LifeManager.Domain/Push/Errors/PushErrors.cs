using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Push.Errors
{
    public static class PushErrors
    {
        public static readonly Error InvalidEndpoint = Error.Validation("Push.InvalidEndpoint", "The subscription's endpoint is not a known push service");
        public static readonly Error InvalidKeys = Error.Validation("Push.InvalidKeys", "The subscription's keys are missing or invalid");
        public static readonly Error LabelTooLong = Error.Validation("Push.LabelTooLong", "The device label cannot be longer than 60 characters");
        public static readonly Error NotConfigured = Error.NotFound("Push.NotConfigured", "Push notifications are not configured on this server");
    }
}
