using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Ranking.Errors
{
    public static class RankingErrors
    {
        public static readonly Error InvalidWeekStart = Error.Validation("Ranking.InvalidWeekStart", "A week must start on a Monday");

        public static readonly Error WeekOutOfRange = Error.Validation("Ranking.WeekOutOfRange", "The week is in the future or before the group was created");

        public static readonly Error MemberNotFound = Error.NotFound("Ranking.MemberNotFound", "Member not found in this group");

        public static readonly Error CannotReactToSelf = Error.Validation("Ranking.CannotReactToSelf", "You can't react to your own week");

        public static readonly Error ReactionOutOfWindow = Error.Validation("Ranking.ReactionOutOfWindow", "Reactions are only open on this week and the last one");

        public static readonly Error UnknownReaction = Error.Validation("Ranking.UnknownReaction", "Unknown reaction");
    }
}
