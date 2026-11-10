using LifeManager.Application.Home.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Home.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class HomeController(HomeOverviewService homeOverviewService) : Controller
    {
        private readonly HomeOverviewService _homeOverviewService = homeOverviewService;

        /// <summary>The day's summary for the Home: habits, player, the current month and its budget. Reads only.</summary>
        [HttpGet("Overview")]
        public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
            => Ok(await _homeOverviewService.GetAsync(User.GetUserId(), cancellationToken));
    }
}
