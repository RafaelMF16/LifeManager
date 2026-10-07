using LifeManager.Application.Habits.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Habits.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Habits/Profile")]
    public class HabitProfileController(PlayerWalletService playerWalletService) : Controller
    {
        private readonly PlayerWalletService _playerWalletService = playerWalletService;

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(await _playerWalletService.GetProfileAsync(User.GetUserId(), cancellationToken));
    }
}
