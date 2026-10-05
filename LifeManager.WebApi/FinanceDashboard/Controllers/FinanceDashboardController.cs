using LifeManager.Application.FinanceDashboard.DTOs;
using LifeManager.Application.FinanceDashboard.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.FinanceDashboard.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FinanceDashboardController(FinanceDashboardService financeDashboardService) : Controller
    {
        private readonly FinanceDashboardService _financeDashboardService = financeDashboardService;

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] FinanceDashboardQueryDto query, CancellationToken cancellationToken)
            => (await _financeDashboardService.GetAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
