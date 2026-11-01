using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Enums;
using LifeManager.Domain.RankingGroups.ValueObjects;

namespace LifeManager.Application.Ranking.DTOs
{
    /// <summary>A line of a group's ranking. <see cref="MemberId"/> is null for someone who has left since (a stored week).</summary>
    /// <param name="Eligible">In the group from the week's Monday (only then does the week count for their achievements).</param>
    /// <param name="ReactedBy">Who reacted to this week: only on the user's own line, null on everyone else's.</param>
    public record RankingEntryDto(
        int? MemberId,
        string Name,
        PlayerAvatar Avatar,
        AchievementCode? Title,
        int Points,
        int PerfectDays,
        int KeptDays,
        int Position,
        bool IsMe,
        bool Eligible,
        bool LeftGroup,
        RankingReactionsDto Reactions,
        IReadOnlyList<RankingReactedByDto>? ReactedBy);

    /// <param name="Mine">The user's own reaction to this member's week, if any.</param>
    public record RankingReactionsDto(int Clap, int Fire, RankingReactionKind? Mine)
    {
        public static readonly RankingReactionsDto None = new(0, 0, null);

        /// <summary>The reactions <paramref name="memberId"/> got in a week, seen by <paramref name="me"/>.</summary>
        public static RankingReactionsDto For(IReadOnlyList<RankingReaction> reactions, RankingGroupMemberId memberId, RankingGroupMemberId? me)
        {
            var received = reactions.Where(reaction => reaction.ToMemberId == memberId).ToList();

            return new RankingReactionsDto(
                received.Count(reaction => reaction.Kind == RankingReactionKind.Clap),
                received.Count(reaction => reaction.Kind == RankingReactionKind.Fire),
                received.FirstOrDefault(reaction => reaction.FromMemberId == me)?.Kind);
        }
    }

    public record RankingReactedByDto(int MemberId, string Name, RankingReactionKind Kind);

    /// <param name="WeekStart">The current week or the one before.</param>
    public record RankingReactionRequestDto(DateOnly? WeekStart, RankingReactionKind Kind);

    /// <param name="Position">Null when the user wasn't in the group that week.</param>
    /// <param name="GapToNext">Points behind the member right ahead; null when first (or out of the week).</param>
    /// <param name="ChallengePoints">The weekly challenges' bonus in the user's live points (always recounted live).</param>
    public record RankingMeDto(int? Position, int Points, int? GapToNext, string? NextName, IReadOnlyList<WeeklyScoreDayDto> Days, int ChallengePoints);

    public record RankingMemberRefDto(int MemberId, string Name);

    /// <summary>Last week's podium (positions 1 to 3, ties included) and the user's line, for the current week's card.</summary>
    public record RankingPreviousDto(DateOnly WeekStart, RankingWeekStatus Status, IReadOnlyList<RankingEntryDto> Podium, int? MyPosition, int? MyPoints);

    /// <param name="FirstWeekStart">The group's first week, the oldest one the navigation reaches.</param>
    /// <param name="OvertakenBy">Who passed the user since yesterday; only in an open week, never on Monday.</param>
    public record GroupRankingDto(
        DateOnly WeekStart,
        DateOnly WeekEnd,
        RankingWeekStatus Status,
        bool CountsForAchievements,
        DateOnly FirstWeekStart,
        IReadOnlyList<RankingEntryDto> Entries,
        RankingMeDto Me,
        IReadOnlyList<RankingMemberRefDto> OvertakenBy,
        RankingPreviousDto? Previous);

    /// <summary>The user's place in one group in a past week, for the weekly summary.</summary>
    /// <param name="MemberCount">The lines of that week's board.</param>
    /// <param name="IsFinal">From the stored result; otherwise live and provisional (the week isn't closed yet).</param>
    public record RankingWeekPlaceDto(int GroupId, string GroupName, int Position, int MemberCount, int Points, bool IsFinal);
}
