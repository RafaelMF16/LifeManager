using LifeManager.Application.Ranking.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Ranking.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RankingController(WeeklyScoreService weeklyScoreService) : Controller
    {
        private readonly WeeklyScoreService _weeklyScoreService = weeklyScoreService;

        /// <summary>The player's ranking points in a week (the current one by default), day by day.</summary>
        [HttpGet("Score")]
        public async Task<IActionResult> GetScore([FromQuery] DateOnly? weekStart, CancellationToken cancellationToken)
            => (await _weeklyScoreService.GetMineAsync(User.GetUserId(), weekStart, cancellationToken)).Match(Ok);
    }
}
