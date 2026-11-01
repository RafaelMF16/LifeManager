using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Habits.Errors
{
    public static class ChallengeErrors
    {
        public static readonly Error NotFound = Error.NotFound("Challenge.NotFound", "Challenge not found");

        public static readonly Error InvalidType = Error.Validation("Challenge.InvalidType", "Unknown challenge type");

        public static readonly Error InvalidWeek = Error.Validation("Challenge.InvalidWeek", "A challenge is for the current or the next week");

        public static readonly Error CreationWindowClosed = Error.Validation(
            "Challenge.CreationWindowClosed", "A challenge for the current week can only be created until Wednesday");

        public static readonly Error HabitNotEligible = Error.Validation(
            "Challenge.HabitNotEligible", "Only an active habit to build can take a challenge");

        public static readonly Error NotMeasurable = Error.Validation(
            "Challenge.NotMeasurable", "A volume challenge needs a habit with a daily target");

        public static readonly Error InvalidTarget = Error.Validation("Challenge.InvalidTarget", "The target is out of range for this week");

        public static readonly Error TargetAlreadyReached = Error.Validation(
            "Challenge.TargetAlreadyReached", "The target must be above what was already done this week");

        public static readonly Error AlreadyExists = Error.Conflict("Challenge.AlreadyExists", "This habit already has a challenge this week");

        public static readonly Error WeekLimitReached = Error.Conflict("Challenge.WeekLimitReached", "The week already has the most challenges allowed");

        public static readonly Error NotActive = Error.Conflict("Challenge.NotActive", "The challenge is no longer active");
    }
}
