using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Habits.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/Habits/Ledger")]
    public class HabitLedgerController(GameLedgerService gameLedgerService) : Controller
    {
        private readonly GameLedgerService _gameLedgerService = gameLedgerService;

        /// <summary>The player's statement of coins, XP and HP, newest first; optionally one habit's entries.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] GameLedgerListQueryDto query, CancellationToken cancellationToken)
            => (await _gameLedgerService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
