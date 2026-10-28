using LifeManager.Application.GameItems.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.GameItems.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class GameItemsController(GameItemService gameItemService) : Controller
    {
        private readonly GameItemService _gameItemService = gameItemService;

        /// <summary>The shop's items with the player's limits and availability, plus today's purchases.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
            => Ok(await _gameItemService.GetAsync(User.GetUserId(), cancellationToken));

        /// <summary>Buys one <paramref name="kind"/> (HpPotion, StreakFreeze) and applies it at once.</summary>
        [HttpPost("{kind}/Purchase")]
        public async Task<IActionResult> Purchase(string kind, CancellationToken cancellationToken)
            => (await _gameItemService.PurchaseAsync(kind, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
