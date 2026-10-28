using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Habits.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Habits/Profile")]
    public class HabitProfileController(PlayerWalletService playerWalletService, PlayerProfileService playerProfileService) : Controller
    {
        private readonly PlayerWalletService _playerWalletService = playerWalletService;
        private readonly PlayerProfileService _playerProfileService = playerProfileService;

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(await _playerWalletService.GetProfileAsync(User.GetUserId(), cancellationToken));

        /// <summary>Picks the avatar and the title (an unlocked achievement's, or none).</summary>
        [HttpPut("Appearance")]
        public async Task<IActionResult> UpdateAppearance(UpdateAppearanceRequestDto request, CancellationToken cancellationToken)
            => (await _playerProfileService.UpdateAppearanceAsync(request, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>The player's lifetime numbers.</summary>
        [HttpGet("Stats")]
        public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
            => Ok(await _playerProfileService.GetStatsAsync(User.GetUserId(), cancellationToken));
    }
}
