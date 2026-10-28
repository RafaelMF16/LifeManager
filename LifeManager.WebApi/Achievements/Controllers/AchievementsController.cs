using LifeManager.Application.Achievements.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Achievements.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class AchievementsController(AchievementService achievementService) : Controller
    {
        private readonly AchievementService _achievementService = achievementService;

        /// <summary>Unlocks what the player's history reaches, then lists every achievement with the progress.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
            => Ok(await _achievementService.GetAsync(User.GetUserId(), cancellationToken));

        /// <summary>Marks every unlock the player was shown as seen; calling it again changes nothing.</summary>
        [HttpPost("Seen")]
        public async Task<IActionResult> MarkSeen(CancellationToken cancellationToken)
        {
            await _achievementService.MarkSeenAsync(User.GetUserId(), cancellationToken);
            return NoContent();
        }
    }
}
