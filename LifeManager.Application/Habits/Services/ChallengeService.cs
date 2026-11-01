using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Shared.DTOs;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Habits.Challenges;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Habits.ValueObjects;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Shared.Paging;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// Weekly challenges: create, preview, give up and list them, with suggestions. Their progress is always recounted
    /// from the stored check-ins and progress entries; <see cref="SyncAsync"/> settles them on read.
    /// </summary>
    public class ChallengeService(
        IWeeklyChallengeRepository challengeRepository,
        IHabitRepository habitRepository,
        IHabitCheckInRepository habitCheckInRepository,
        IHabitProgressRepository habitProgressRepository,
        AppClock appClock,
        TimeProvider timeProvider)
    {
        private readonly IWeeklyChallengeRepository _challengeRepository = challengeRepository;
        private readonly IHabitRepository _habitRepository = habitRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly IHabitProgressRepository _habitProgressRepository = habitProgressRepository;
        private readonly AppClock _appClock = appClock;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<ChallengeWeekDto>> GetWeekAsync(UserId userId, ChallengeWeek week, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(week))
                return ChallengeErrors.InvalidWeek;

            await SyncAsync(userId, cancellationToken);

            var today = _appClock.Today();
            var weekStart = ChallengeRules.WeekStartFor(week, today);
            var stored = (await _challengeRepository.GetByWeekAsync(userId, weekStart, cancellationToken))
                .Where(challenge => challenge.Status != ChallengeStatus.Cancelled)
                .ToList();
            var figures = await ReadFiguresAsync(userId, weekStart, cancellationToken);
            var habits = await _habitRepository.GetActiveByUserIdAsync(userId, HabitKind.Positive, cancellationToken);
            var named = await HabitsAsync(userId, stored, cancellationToken);

            var challenges = stored.Select(challenge => ToDto(challenge, named[challenge.HabitId], figures, today)).ToList();

            var taken = stored.Select(challenge => challenge.HabitId).ToHashSet();
            var isOpen = ChallengeRules.EnsureWeekIsOpen(weekStart, today).IsSuccess;
            var options = isOpen && stored.Count < GameRules.MaxChallengesPerWeek
                ? habits.Where(habit => !taken.Contains(habit.Id!) && habit.StartDate <= GameWeek.End(weekStart))
                    .Select(habit => Option(habit, weekStart, today, figures))
                    .Where(option => option.MaxFrequency > option.FrequencyProgress || option.MaxVolume > option.VolumeProgress)
                    .ToList()
                : [];

            var habitsById = habits.ToDictionary(habit => habit.Id!);
            var suggestions = options.Count == 0
                ? []
                : ChallengeRules.Suggest(options.Select(option => Candidate(habitsById[new HabitId(option.HabitId)], weekStart, today, figures)), stored.Count)
                    .Select(suggestion => ToDto(suggestion, habitsById[suggestion.HabitId]))
                    .ToList();

            return new ChallengeWeekDto(week, weekStart, GameWeek.End(weekStart), isOpen, options.Count > 0, challenges, suggestions, options);
        }

        public async Task<Result<ChallengePreviewDto>> PreviewAsync(UserId userId, CreateChallengeDto request, CancellationToken cancellationToken)
        {
            var draft = await DraftAsync(userId, request, cancellationToken);
            if (!draft.IsSuccess)
                return draft.Error!;

            var challenge = WeeklyChallenge.Create(draft.Value!, _timeProvider.GetUtcNow());
            if (!challenge.IsSuccess)
                return challenge.Error!;

            var created = challenge.Value!;

            return new ChallengePreviewDto(created.Effort, created.RewardCoins, created.RewardXp, created.Baseline);
        }

        public async Task<Result<ChallengeDto>> CreateAsync(UserId userId, CreateChallengeDto request, CancellationToken cancellationToken)
        {
            var draft = await DraftAsync(userId, request, cancellationToken);
            if (!draft.IsSuccess)
                return draft.Error!;

            var challenge = WeeklyChallenge.Create(draft.Value!, _timeProvider.GetUtcNow());
            if (!challenge.IsSuccess)
                return challenge.Error!;

            var added = await _challengeRepository.AddAsync(challenge.Value!, cancellationToken);
            if (!added.IsSuccess)
                return added.Error!;

            var created = challenge.Value!;
            created.AssignId(added.Value!.Value);

            return ToDto(created, draft.Value!.Habit, draft.Value.Progress, draft.Value.Today);
        }

        /// <summary>Gives up an active challenge: it is deleted, pays nothing and counts for nothing.</summary>
        public async Task<Result> DeleteAsync(UserId userId, int id, CancellationToken cancellationToken)
        {
            var challengeId = new WeeklyChallengeId(id);
            var challenge = await _challengeRepository.GetByIdAsync(challengeId, userId, cancellationToken);
            if (challenge is null)
                return ChallengeErrors.NotFound;

            var allowed = challenge.EnsureCanDelete();
            if (!allowed.IsSuccess)
                return allowed;

            return await _challengeRepository.DeleteActiveAsync(challengeId, userId, cancellationToken)
                ? Result.Success()
                : ChallengeErrors.NotActive;
        }

        /// <summary>The weeks before the current one, newest first.</summary>
        public async Task<Result<PagedResponseDto<ChallengeDto>>> GetHistoryAsync(UserId userId, ChallengeHistoryQueryDto query, CancellationToken cancellationToken)
        {
            var pageRequestResult = PageRequest.Create(query.Page, query.PageSize);
            if (!pageRequestResult.IsSuccess)
                return pageRequestResult.Error!;

            var pageRequest = pageRequestResult.Value!;
            await SyncAsync(userId, cancellationToken);

            var today = _appClock.Today();
            var page = await _challengeRepository.GetHistoryAsync(userId, GameWeek.Start(today), pageRequest, cancellationToken);
            if (page.Items.Count == 0)
                return PagedResponseDto<ChallengeDto>.From(page, _ => null!);

            var from = page.Items.Min(challenge => challenge.WeekStart);
            var to = GameWeek.End(page.Items.Max(challenge => challenge.WeekStart));
            var figures = await ReadFiguresAsync(userId, from, to, cancellationToken);
            var named = await HabitsAsync(userId, page.Items, cancellationToken);

            return PagedResponseDto<ChallengeDto>.From(page, challenge => ToDto(challenge, named[challenge.HabitId], figures, today));
        }

        /// <summary>
        /// Settles the active challenges: completes (and pays, once) those that reached their target, fails those whose
        /// week can no longer change (from Tuesday on) and cancels those of an archived habit.
        /// </summary>
        /// <returns>The challenges this call completed.</returns>
        public async Task<IReadOnlyList<WeeklyChallenge>> SyncAsync(UserId userId, CancellationToken cancellationToken)
        {
            var active = await _challengeRepository.GetActiveAsync(userId, cancellationToken);
            if (active.Count == 0)
                return [];

            var today = _appClock.Today();
            var habits = (await _habitRepository.GetByIdsAsync(userId, [.. active.Select(challenge => challenge.HabitId).Distinct()], cancellationToken))
                .ToDictionary(habit => habit.Id!);
            var figures = await ReadFiguresAsync(
                userId, active.Min(challenge => challenge.WeekStart), GameWeek.End(active.Max(challenge => challenge.WeekStart)), cancellationToken);

            var cancelled = new List<WeeklyChallengeId>();
            var failed = new List<WeeklyChallengeId>();
            var reached = new List<WeeklyChallenge>();

            foreach (var challenge in active)
            {
                if (!habits.TryGetValue(challenge.HabitId, out var habit) || habit.IsArchived)
                    cancelled.Add(challenge.Id!);
                else if (figures.Progress(challenge) >= challenge.Target)
                    reached.Add(challenge);
                else if (ChallengeProgress.IsOver(challenge.WeekStart, today))
                    failed.Add(challenge.Id!);
            }

            await _challengeRepository.EndAsync(cancelled, ChallengeStatus.Cancelled, cancellationToken);
            await _challengeRepository.EndAsync(failed, ChallengeStatus.Failed, cancellationToken);
            if (reached.Count == 0)
                return [];

            var now = _timeProvider.GetUtcNow();

            return await _challengeRepository.CompleteAsync(userId, reached, now, (lockedProfile, challenge) =>
            {
                var outcome = lockedProfile.Apply(new GameDelta(challenge.RewardCoins, challenge.RewardXp, 0));

                return GameLedgerEntry.FromOutcome(
                    userId, GameLedgerEntryKind.ChallengeCompleted, today, now, habits[challenge.HabitId].Name.Value, outcome,
                    challenge.HabitId, challengeId: challenge.Id);
            }, cancellationToken);
        }

        /// <summary>For the day's checklist: the current week's challenges, settled first.</summary>
        public async Task<IReadOnlyList<ChallengeDto>> GetCurrentWeekAsync(UserId userId, CancellationToken cancellationToken)
        {
            await SyncAsync(userId, cancellationToken);

            return await GetWeekDtosAsync(userId, GameWeek.Start(_appClock.Today()), cancellationToken);
        }

        /// <summary>A week's challenges as they are stored (given up ones left out), without settling them first.</summary>
        public async Task<IReadOnlyList<ChallengeDto>> GetWeekDtosAsync(UserId userId, DateOnly weekStart, CancellationToken cancellationToken)
        {
            var week = (await _challengeRepository.GetByWeekAsync(userId, weekStart, cancellationToken))
                .Where(challenge => challenge.Status != ChallengeStatus.Cancelled)
                .ToList();

            return await ToDtosAsync(userId, week, _appClock.Today(), cancellationToken);
        }

        /// <summary>
        /// For the player's profile: settles the challenges, then lists the completions not celebrated yet (any week), so
        /// the frontend celebrates each one once, on any device.
        /// </summary>
        public async Task<IReadOnlyList<ChallengeDto>> SyncAndGetUnseenAsync(UserId userId, CancellationToken cancellationToken)
        {
            await SyncAsync(userId, cancellationToken);

            var unseen = await _challengeRepository.GetUnseenCompletedAsync(userId, cancellationToken);

            return await ToDtosAsync(userId, unseen, _appClock.Today(), cancellationToken);
        }

        private async Task<IReadOnlyList<ChallengeDto>> ToDtosAsync(
            UserId userId, IReadOnlyList<WeeklyChallenge> challenges, DateOnly today, CancellationToken cancellationToken)
        {
            if (challenges.Count == 0)
                return [];

            var figures = await ReadFiguresAsync(
                userId, challenges.Min(challenge => challenge.WeekStart), GameWeek.End(challenges.Max(challenge => challenge.WeekStart)), cancellationToken);
            var named = await HabitsAsync(userId, challenges, cancellationToken);

            return [.. challenges.Select(challenge => ToDto(challenge, named[challenge.HabitId], figures, today))];
        }

        /// <summary>Marks the completions the frontend celebrated, so each one is celebrated once, on any device.</summary>
        public Task MarkSeenAsync(UserId userId, IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
            => ids.Count == 0
                ? Task.CompletedTask
                : _challengeRepository.MarkSeenAsync(userId, [.. ids.Select(id => new WeeklyChallengeId(id))], _timeProvider.GetUtcNow(), cancellationToken);

        private async Task<Result<ChallengeDraft>> DraftAsync(UserId userId, CreateChallengeDto request, CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(request.Week))
                return ChallengeErrors.InvalidWeek;

            var habit = await _habitRepository.GetByIdAsync(new HabitId(request.HabitId), userId, cancellationToken);
            if (habit is null)
                return HabitErrors.NotFound;

            var today = _appClock.Today();
            var weekStart = ChallengeRules.WeekStartFor(request.Week, today);
            var figures = await ReadFiguresAsync(userId, weekStart, cancellationToken);
            var progress = request.Type == ChallengeType.Volume
                ? figures.Volume(habit.Id!, weekStart)
                : figures.Frequency(habit.Id!, weekStart);

            return new ChallengeDraft(
                habit, request.Type, request.Target, weekStart, today, progress, figures.Baseline(habit, request.Type, today),
                figures.DoneDates(habit.Id!, weekStart));
        }

        private static ChallengeHabitOptionDto Option(Habit habit, DateOnly weekStart, DateOnly today, ChallengeFigures figures)
            => new(
                habit.Id!.Value,
                habit.Name.Value,
                habit.TargetUnit,
                ChallengeRules.MaxFrequencyTarget(habit, weekStart, today, figures.DoneDates(habit.Id!, weekStart)),
                habit.Target is null ? null : ChallengeRules.MaxVolumeTarget(habit),
                figures.Frequency(habit.Id!, weekStart),
                figures.Volume(habit.Id!, weekStart));

        private static ChallengeCandidate Candidate(Habit habit, DateOnly weekStart, DateOnly today, ChallengeFigures figures)
            => new(
                habit,
                figures.Baseline(habit, ChallengeType.Frequency, today),
                habit.Target is null ? null : figures.Baseline(habit, ChallengeType.Volume, today),
                ChallengeRules.PossibleDays(habit, weekStart),
                ChallengeRules.MaxFrequencyTarget(habit, weekStart, today, figures.DoneDates(habit.Id!, weekStart)),
                figures.Frequency(habit.Id!, weekStart),
                figures.Volume(habit.Id!, weekStart));

        /// <summary>The figures for a week, plus the closed weeks a baseline averages.</summary>
        private Task<ChallengeFigures> ReadFiguresAsync(UserId userId, DateOnly weekStart, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var from = GameWeek.Start(today).AddDays(-GameWeek.Days * GameRules.ChallengeBaselineWeeks);

            return ReadFiguresAsync(userId, from < weekStart ? from : weekStart, GameWeek.End(weekStart), cancellationToken);
        }

        private async Task<ChallengeFigures> ReadFiguresAsync(UserId userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
        {
            var checkIns = await _habitCheckInRepository.GetByUserIdAsync(userId, from, to, cancellationToken);
            var entries = await _habitProgressRepository.GetByUserIdAsync(userId, from, to, cancellationToken);

            return new ChallengeFigures(checkIns, entries);
        }

        /// <summary>The challenges' habits (archived ones included), for their names and units.</summary>
        private async Task<IReadOnlyDictionary<HabitId, Habit>> HabitsAsync(UserId userId, IEnumerable<WeeklyChallenge> challenges, CancellationToken cancellationToken)
        {
            var habits = await _habitRepository.GetByIdsAsync(userId, [.. challenges.Select(challenge => challenge.HabitId).Distinct()], cancellationToken);

            return habits.ToDictionary(habit => habit.Id!);
        }

        private static ChallengeDto ToDto(WeeklyChallenge challenge, Habit habit, ChallengeFigures figures, DateOnly today)
            => ToDto(challenge, habit, figures.Progress(challenge), today);

        private static ChallengeDto ToDto(WeeklyChallenge challenge, Habit habit, int progress, DateOnly today)
            => new(
                challenge.Id!.Value,
                challenge.HabitId.Value,
                habit.Name.Value,
                challenge.Type,
                challenge.Target,
                challenge.Type == ChallengeType.Volume ? habit.TargetUnit : null,
                progress,
                ChallengeProgress.DaysLeft(challenge.WeekStart, today),
                challenge.WeekStart,
                challenge.Baseline,
                challenge.Effort,
                challenge.RewardCoins,
                challenge.RewardXp,
                challenge.Status,
                challenge.CompletedAt);

        private static ChallengeSuggestionDto ToDto(ChallengeSuggestion suggestion, Habit habit)
            => new(
                suggestion.HabitId.Value,
                habit.Name.Value,
                suggestion.Type,
                suggestion.Target,
                suggestion.Type == ChallengeType.Volume ? habit.TargetUnit : null,
                suggestion.Baseline,
                suggestion.Effort,
                suggestion.Reward.Coins,
                suggestion.Reward.Xp);
    }
}
