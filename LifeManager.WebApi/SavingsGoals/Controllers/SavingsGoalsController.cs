using LifeManager.Application.SavingsGoals.DTOs;
using LifeManager.Application.SavingsGoals.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.SavingsGoals.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class SavingsGoalsController(SavingsGoalService savingsGoalService) : Controller
    {
        private readonly SavingsGoalService _savingsGoalService = savingsGoalService;

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _savingsGoalService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SavingsGoalDto goalDto, CancellationToken cancellationToken)
            => (await _savingsGoalService.CreateAsync(goalDto, User.GetUserId(), cancellationToken))
                .Match(goal => CreatedAtAction(nameof(GetById), new { id = goal.Id }, goal));
    }
}
