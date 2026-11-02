using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Ranking.Services;
using LifeManager.Application.Shared.Time;
using LifeManager.Domain.Habits.Enums;
using LifeManager.Domain.Habits.Errors;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.Ranking;
using LifeManager.Domain.Shared.Results;
using LifeManager.Domain.Users.ValueObjects;

namespace LifeManager.Application.Habits.Services
{
    /// <summary>
    /// The weekly summary: last week's challenges and figures next to the week before, and the player's place in each
    /// ranking group; shown once per week on any device (<c>PlayerProfile.SummarySeenWeek</c>), and only to a player who
    /// played that week (being in a group doesn't count).
    /// </summary>
    public class WeeklySummaryService(
        ChallengeService challengeService,
        IWeeklyChallengeRepository challengeRepository,
        IHabitCheckInRepository habitCheckInRepository,
        IGameLedgerRepository gameLedgerRepository,
        IPlayerProfileRepository playerProfileRepository,
        GroupRankingService groupRankingService,
        AppClock appClock)
    {
        private readonly ChallengeService _challengeService = challengeService;
        private readonly IWeeklyChallengeRepository _challengeRepository = challengeRepository;
        private readonly IHabitCheckInRepository _habitCheckInRepository = habitCheckInRepository;
        private readonly IGameLedgerRepository _gameLedgerRepository = gameLedgerRepository;
        private readonly IPlayerProfileRepository _playerProfileRepository = playerProfileRepository;
        private readonly GroupRankingService _groupRankingService = groupRankingService;
        private readonly AppClock _appClock = appClock;

        /// <summary>Last week's summary; null when it was already seen or the player didn't play (no check-in, no challenge).</summary>
        public async Task<WeeklySummaryDto?> GetAsync(UserId userId, CancellationToken cancellationToken)
        {
            var today = _appClock.Today();
            var weekStart = GameWeek.Start(today).AddDays(-GameWeek.Days);

            var profile = await _playerProfileRepository.GetByUserIdAsync(userId, cancellationToken);
            if (profile is null || profile.SummarySeenWeek >= weekStart)
                return null;

            // Settles first, so a challenge Sunday's check-in completed is paid and shown as completed.
            await _challengeService.SyncAsync(userId, cancellationToken);

            var challenges = await _challengeService.GetWeekDtosAsync(userId, weekStart, cancellationToken);
            var week = await FiguresAsync(userId, weekStart, cancellationToken);
            if (week.CheckIns == 0 && week.CleanDays == 0 && challenges.Count == 0)
                return null;

            var previousWeek = await FiguresAsync(userId, weekStart.AddDays(-GameWeek.Days), cancellationToken);
            var ranking = await _groupRankingService.GetWeekPlacesAsync(userId, weekStart, cancellationToken);

            return new WeeklySummaryDto(weekStart, GameWeek.End(weekStart), week, previousWeek, challenges, ranking);
        }

        /// <summary>Marks a summary as seen; only a week before the current one can be.</summary>
        public async Task<Result> MarkSeenAsync(UserId userId, WeeklySummarySeenDto request, CancellationToken cancellationToken)
        {
            if (!GameWeek.IsWeekStart(request.WeekStart) || request.WeekStart >= GameWeek.Start(_appClock.Today()))
                return ChallengeErrors.InvalidSummaryWeek;

            await _playerProfileRepository.MarkSummarySeenAsync(userId, request.WeekStart, cancellationToken);

            return Result.Success();
        }

        private async Task<WeeklySummaryFiguresDto> FiguresAsync(UserId userId, DateOnly weekStart, CancellationToken cancellationToken)
        {
            var weekEnd = GameWeek.End(weekStart);
            var checkIns = await _habitCheckInRepository.GetByUserIdAsync(userId, weekStart, weekEnd, cancellationToken);
            var earnings = await _gameLedgerRepository.GetHabitEarningsAsync(userId, weekStart, weekEnd, cancellationToken);
            var completed = (await _challengeRepository.GetByWeekAsync(userId, weekStart, cancellationToken))
                .Where(challenge => challenge.Status == ChallengeStatus.Completed)
                .ToList();

            return new WeeklySummaryFiguresDto(
                checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Done),
                checkIns.Count(checkIn => checkIn.Status == HabitCheckInStatus.Clean),
                earnings.Coins + completed.Sum(challenge => challenge.RewardCoins),
                completed.Count);
        }
    }
}
