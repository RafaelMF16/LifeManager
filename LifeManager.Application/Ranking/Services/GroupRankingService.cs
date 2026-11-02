using LifeManager.Application.Ranking.DTOs;
using LifeManager.Application.RankingGroups.DTOs;
using LifeManager.Application.RankingGroups.Services;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Achievements.Enums;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Ranking.Enums;
using LifeManager.Domain.Ranking.Errors;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.Errors;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Ranking.Services
{
    /// <summary>
    /// A group's weekly ranking, the same for every member: live from the check-ins while the week isn't stored
    /// (<see cref="RankingWeekStatus.Open"/>/<see cref="RankingWeekStatus.Closing"/>), the stored result once
    /// <see cref="RankingWeekCloseService"/> closed it.
    /// </summary>
    public class GroupRankingService(
        IRankingGroupRepository rankingGroupRepository,
        IRankingWeekResultRepository rankingWeekResultRepository,
        IRankingReactionRepository rankingReactionRepository,
        RankingGroupService rankingGroupService,
        WeeklyScoreService weeklyScoreService,
        AppClock appClock)
    {
        private readonly IRankingGroupRepository _rankingGroupRepository = rankingGroupRepository;
        private readonly IRankingWeekResultRepository _rankingWeekResultRepository = rankingWeekResultRepository;
        private readonly IRankingReactionRepository _rankingReactionRepository = rankingReactionRepository;
        private readonly RankingGroupService _rankingGroupService = rankingGroupService;
        private readonly WeeklyScoreService _weeklyScoreService = weeklyScoreService;
        private readonly AppClock _appClock = appClock;

        /// <summary>The user's groups with their place and points in each this week.</summary>
        public async Task<IReadOnlyList<RankingGroupListItemDto>> GetGroupsAsync(UserId userId, CancellationToken cancellationToken)
        {
            var groups = await _rankingGroupService.GetAllAsync(userId, cancellationToken);
            if (groups.Count == 0)
                return groups;

            var today = _appClock.Today();
            var members = await _rankingGroupRepository.GetMembersOfUserGroupsAsync(userId, cancellationToken);
            var scores = await _weeklyScoreService.GetForUsersAsync(
                [.. members.Select(member => member.UserId)], GameWeek.Start(today), today, cancellationToken);

            return [.. groups.Select(group =>
            {
                var standings = GroupStandings.Rank(members
                    .Where(member => member.GroupId.Value == group.Id)
                    .Select(member => (member.UserId, scores[member.UserId])));
                var mine = standings.SingleOrDefault(standing => standing.UserId == userId);

                return group with { MyPosition = mine?.Position, MyPoints = mine?.Points ?? 0 };
            })];
        }

        /// <summary>
        /// The user's place in each group they are in now, in <paramref name="week"/>: the stored result once the week is
        /// closed, live (provisional) before. A group created after that week, or one the user joined after it, is left
        /// out. Best place first.
        /// </summary>
        public async Task<IReadOnlyList<RankingWeekPlaceDto>> GetWeekPlacesAsync(UserId userId, DateOnly week, CancellationToken cancellationToken)
        {
            var groups = await _rankingGroupService.GetAllAsync(userId, cancellationToken);
            var today = _appClock.Today();
            var places = new List<RankingWeekPlaceDto>();

            foreach (var item in groups)
            {
                var group = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(item.Id), userId, cancellationToken);
                if (group is null || GameWeek.Start(group.Group.CreatedOn) > week)
                    continue;

                var board = await GetBoardAsync(group, week, today, cancellationToken);
                var mine = board.Lines.SingleOrDefault(line => line.UserId == userId);
                if (mine is null)
                    continue;

                places.Add(new RankingWeekPlaceDto(
                    item.Id, item.Name, mine.Standing.Position, board.Lines.Count, mine.Standing.Points, board.Status == RankingWeekStatus.Final));
            }

            return [.. places.OrderBy(place => place.Position).ThenBy(place => place.GroupName, StringComparer.CurrentCultureIgnoreCase)];
        }

        public async Task<Result<GroupRankingDto>> GetAsync(int groupId, UserId userId, DateOnly? weekStart, CancellationToken cancellationToken)
        {
            var group = await _rankingGroupRepository.GetForMemberAsync(new RankingGroupId(groupId), userId, cancellationToken);
            if (group is null)
                return RankingGroupErrors.NotFound;

            var today = _appClock.Today();
            var currentWeek = GameWeek.Start(today);
            var firstWeek = GameWeek.Start(group.Group.CreatedOn);
            var week = weekStart ?? currentWeek;
            if (!GameWeek.IsWeekStart(week))
                return RankingErrors.InvalidWeekStart;
            if (week > currentWeek || week < firstWeek)
                return RankingErrors.WeekOutOfRange;

            var board = await GetBoardAsync(group, week, today, cancellationToken);
            var reactions = await GetReactionsAsync(group, week, userId, cancellationToken);
            var lines = board.Lines;
            var mine = lines.SingleOrDefault(line => line.UserId == userId);
            var next = mine is null ? null : GroupStandings.Next([.. lines.Select(line => line.Standing)], userId);
            var nextLine = next is null ? null : lines.First(line => line.UserId == next.UserId);
            // A live board already scored the user over the same days; a stored week keeps no days, so read them.
            var myScore = board.Scores.TryGetValue(userId, out var scored)
                ? scored
                : (await _weeklyScoreService.GetForUsersAsync([userId], week, Min(today, GameWeek.End(week)), cancellationToken))[userId];

            var me = new RankingMeDto(
                mine?.Standing.Position,
                mine?.Standing.Points ?? 0,
                nextLine is null ? null : nextLine.Standing.Points - mine!.Standing.Points,
                nextLine?.Name,
                [.. myScore.Days.Select(WeeklyScoreDayDto.From)],
                myScore.ChallengePoints);

            var previous = week == currentWeek && week > firstWeek
                ? await GetPreviousAsync(group, week.AddDays(-GameWeek.Days), today, userId, cancellationToken)
                : null;

            return new GroupRankingDto(
                week,
                GameWeek.End(week),
                board.Status,
                GroupStandings.CountsForAchievements(board.EligibleCount),
                firstWeek,
                [.. lines.Select(line => ToEntry(line, userId, reactions))],
                me,
                await GetOvertakenByAsync(board, week, today, userId, cancellationToken),
                previous);
        }

        private async Task<RankingPreviousDto> GetPreviousAsync(RankingGroupView group, DateOnly week, DateOnly today, UserId userId, CancellationToken cancellationToken)
        {
            var board = await GetBoardAsync(group, week, today, cancellationToken);
            var reactions = await GetReactionsAsync(group, week, userId, cancellationToken);
            var mine = board.Lines.SingleOrDefault(line => line.UserId == userId);

            return new RankingPreviousDto(
                week,
                board.Status,
                [.. board.Lines.Where(line => line.Standing.Position <= RankingRules.PodiumPositions).Select(line => ToEntry(line, userId, reactions))],
                mine?.Standing.Position,
                mine?.Standing.Points);
        }

        /// <summary>The week's lines, best first and by name within a tie: the stored result, or live from the check-ins.</summary>
        private async Task<Board> GetBoardAsync(RankingGroupView group, DateOnly week, DateOnly today, CancellationToken cancellationToken)
        {
            var stored = await _rankingWeekResultRepository.GetWeekAsync(group.Group.Id!, week, cancellationToken);
            if (stored.Count > 0)
            {
                var finalLines = stored.Select(view => new Line(
                    view.Result.UserId,
                    view.MemberId,
                    view.Name,
                    view.Avatar,
                    view.Title,
                    new Standing(view.Result.UserId, view.Result.Points, view.Result.PerfectDays, view.Result.KeptDays, view.Result.Position),
                    view.Result.Eligible,
                    view.MemberId is null));

                return new Board(RankingWeekStatus.Final, Order(finalLines), stored[0].Result.EligibleCount, [], new Dictionary<UserId, WeeklyScoreResult>());
            }

            // Like the close: the current members, only those already in by the week's end.
            var sunday = GameWeek.End(week);
            var members = group.Members.Where(member => member.JoinedOn <= sunday).ToList();
            var userIds = members.Select(member => member.UserId).ToList();
            var scores = await _weeklyScoreService.GetForUsersAsync(userIds, week, Min(today, sunday), cancellationToken);
            var standings = GroupStandings.Rank(scores.Select(score => (score.Key, score.Value)));

            var lines = standings.Select(standing =>
            {
                var member = members.First(candidate => candidate.UserId == standing.UserId);

                return new Line(
                    member.UserId, member.MemberId, member.Name, member.Avatar, member.Title, standing,
                    GroupStandings.IsEligible(member.JoinedOn, week), false);
            });

            var status = week == GameWeek.Start(today) ? RankingWeekStatus.Open : RankingWeekStatus.Closing;
            var ordered = Order(lines);

            return new Board(status, ordered, ordered.Count(line => line.Eligible), members, scores);
        }

        /// <summary>Who passed the user since yesterday: an open week only, and never on its Monday (no yesterday in it).</summary>
        private async Task<IReadOnlyList<RankingMemberRefDto>> GetOvertakenByAsync(
            Board board, DateOnly week, DateOnly today, UserId userId, CancellationToken cancellationToken)
        {
            if (board.Status != RankingWeekStatus.Open || today == week)
                return [];

            var userIds = board.Members.Select(member => member.UserId).ToList();
            var yesterday = await _weeklyScoreService.GetForUsersAsync(userIds, week, today.AddDays(-1), cancellationToken);
            var before = GroupStandings.Rank(yesterday.Select(score => (score.Key, score.Value)));
            var passed = GroupStandings.OvertakenBy(before, [.. board.Lines.Select(line => line.Standing)], userId);

            return [.. board.Lines
                .Where(line => passed.Contains(line.UserId))
                .Select(line => new RankingMemberRefDto(line.MemberId!.Value, line.Name))];
        }

        private async Task<WeekReactions> GetReactionsAsync(RankingGroupView group, DateOnly week, UserId userId, CancellationToken cancellationToken)
        {
            var reactions = await _rankingReactionRepository.GetWeekAsync(group.Group.Id!, week, cancellationToken);

            return new WeekReactions(
                reactions,
                group.Members.Single(member => member.UserId == userId).MemberId,
                group.Members.ToDictionary(member => member.MemberId, member => member.Name));
        }

        private static List<Line> Order(IEnumerable<Line> lines)
            => [.. lines
                .OrderBy(line => line.Standing.Position)
                .ThenBy(line => line.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(line => line.UserId.Value)];

        private static RankingEntryDto ToEntry(Line line, UserId userId, WeekReactions reactions)
        {
            var isMe = line.UserId == userId;

            // Who reacted is only shown to the one who got the reactions. Someone who left took theirs with them.
            IReadOnlyList<RankingReactedByDto>? reactedBy = isMe
                ? [.. reactions.All
                    .Where(reaction => reaction.ToMemberId == reactions.Me)
                    .Select(reaction => new RankingReactedByDto(reaction.FromMemberId.Value, reactions.Names[reaction.FromMemberId], reaction.Kind))]
                : null;

            return new(
                line.MemberId?.Value,
                line.Name,
                line.Avatar,
                line.Title,
                line.Standing.Points,
                line.Standing.PerfectDays,
                line.Standing.KeptDays,
                line.Standing.Position,
                isMe,
                line.Eligible,
                line.LeftGroup,
                line.MemberId is null ? RankingReactionsDto.None : RankingReactionsDto.For(reactions.All, line.MemberId, reactions.Me),
                reactedBy);
        }

        private static DateOnly Min(DateOnly a, DateOnly b)
            => a < b ? a : b;

        private sealed record Line(
            UserId UserId,
            RankingGroupMemberId? MemberId,
            string Name,
            PlayerAvatar Avatar,
            AchievementCode? Title,
            Standing Standing,
            bool Eligible,
            bool LeftGroup);

        private sealed record WeekReactions(
            IReadOnlyList<RankingReaction> All, RankingGroupMemberId Me, IReadOnlyDictionary<RankingGroupMemberId, string> Names);

        /// <param name="Members">The members a live board was scored for (empty for a stored week).</param>
        /// <param name="Scores">Their scores up to today or the week's Sunday (empty for a stored week).</param>
        private sealed record Board(
            RankingWeekStatus Status,
            IReadOnlyList<Line> Lines,
            int EligibleCount,
            IReadOnlyList<RankingMemberView> Members,
            IReadOnlyDictionary<UserId, WeeklyScoreResult> Scores);
    }
}
