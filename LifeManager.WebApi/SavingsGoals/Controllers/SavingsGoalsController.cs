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

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] SavingsGoalListQueryDto query, CancellationToken cancellationToken)
            => (await _savingsGoalService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _savingsGoalService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] SavingsGoalDto goalDto, CancellationToken cancellationToken)
            => (await _savingsGoalService.CreateAsync(goalDto, User.GetUserId(), cancellationToken))
                .Match(goal => CreatedAtAction(nameof(GetById), new { id = goal.Id }, goal));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SavingsGoalDto goalDto, CancellationToken cancellationToken)
            => (await _savingsGoalService.UpdateAsync(id, goalDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Restore")]
        public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
            => (await _savingsGoalService.RestoreAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Archives the goal: its investments are kept and it can be restored.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _savingsGoalService.ArchiveAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
