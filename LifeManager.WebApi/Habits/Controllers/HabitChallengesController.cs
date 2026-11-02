using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.Domain.Habits.Enums;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Habits.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Habits/Challenges")]
    public class HabitChallengesController(ChallengeService challengeService, WeeklySummaryService weeklySummaryService) : Controller
    {
        private readonly ChallengeService _challengeService = challengeService;
        private readonly WeeklySummaryService _weeklySummaryService = weeklySummaryService;

        /// <summary>The week's challenges with their progress, the suggestions and the habits that can still take one.</summary>
        [HttpGet]
        public async Task<IActionResult> GetWeek([FromQuery] ChallengeWeek week, CancellationToken cancellationToken)
            => (await _challengeService.GetWeekAsync(User.GetUserId(), week == 0 ? ChallengeWeek.Current : week, cancellationToken)).Match(Ok);

        /// <summary>The effort, reward and baseline a challenge would have, without creating it.</summary>
        [HttpGet("Preview")]
        public async Task<IActionResult> Preview([FromQuery] CreateChallengeDto request, CancellationToken cancellationToken)
            => (await _challengeService.PreviewAsync(User.GetUserId(), request, cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChallengeDto request, CancellationToken cancellationToken)
            => (await _challengeService.CreateAsync(User.GetUserId(), request, cancellationToken))
                .Match(challenge => StatusCode(StatusCodes.Status201Created, challenge));

        /// <summary>Gives up an active challenge.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _challengeService.DeleteAsync(User.GetUserId(), id, cancellationToken)).Match(NoContent);

        /// <summary>The weeks before the current one, newest first.</summary>
        [HttpGet("History")]
        public async Task<IActionResult> GetHistory([FromQuery] ChallengeHistoryQueryDto query, CancellationToken cancellationToken)
            => (await _challengeService.GetHistoryAsync(User.GetUserId(), query, cancellationToken)).Match(Ok);

        /// <summary>Marks the completions the player was shown as seen; calling it again changes nothing.</summary>
        [HttpPost("Seen")]
        public async Task<IActionResult> MarkSeen(ChallengesSeenDto request, CancellationToken cancellationToken)
            => (await _challengeService.MarkSeenAsync(User.GetUserId(), request.Ids ?? [], cancellationToken)).Match(NoContent);

        /// <summary>Last week's summary, or 204 when it was already seen or the player didn't play that week.</summary>
        [HttpGet("Summary")]
        public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        {
            var summary = await _weeklySummaryService.GetAsync(User.GetUserId(), cancellationToken);
            return summary is null ? NoContent() : Ok(summary);
        }

        /// <summary>Marks a weekly summary as seen, on every device.</summary>
        [HttpPost("Summary/Seen")]
        public async Task<IActionResult> MarkSummarySeen(WeeklySummarySeenDto request, CancellationToken cancellationToken)
            => (await _weeklySummaryService.MarkSeenAsync(User.GetUserId(), request, cancellationToken)).Match(NoContent);
    }
}
