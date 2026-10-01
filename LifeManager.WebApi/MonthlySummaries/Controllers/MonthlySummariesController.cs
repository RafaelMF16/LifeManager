using LifeManager.Application.MonthlySummaries.DTOs;
using LifeManager.Application.MonthlySummaries.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.MonthlySummaries.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class MonthlySummariesController(MonthlySummaryService monthlySummaryService) : Controller
    {
        private readonly MonthlySummaryService _monthlySummaryService = monthlySummaryService;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] MonthlySummaryListQueryDto query, CancellationToken cancellationToken)
            => (await _monthlySummaryService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpGet("Years")]
        public async Task<IActionResult> GetYears(CancellationToken cancellationToken)
            => (await _monthlySummaryService.GetYearsAsync(User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _monthlySummaryService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] MonthlySummaryDto monthlySummaryDto, CancellationToken cancellationToken)
            => (await _monthlySummaryService.CreateAsync(monthlySummaryDto, User.GetUserId(), cancellationToken))
                .Match(monthlySummary => CreatedAtAction(nameof(GetById), new { id = monthlySummary.Id }, monthlySummary));
    }
}
