using LifeManager.Application.Budgets.DTOs;
using LifeManager.Application.Budgets.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Budgets.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class BudgetsController(BudgetService budgetService) : Controller
    {
        private readonly BudgetService _budgetService = budgetService;

        [HttpGet]
        public async Task<IActionResult> GetMonth([FromQuery] BudgetMonthQueryDto query, CancellationToken cancellationToken)
            => (await _budgetService.GetMonthAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPut]
        public async Task<IActionResult> Set([FromBody] BudgetDto budgetDto, CancellationToken cancellationToken)
            => (await _budgetService.SetAsync(budgetDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Remove(int id, [FromQuery] BudgetMonthQueryDto query, CancellationToken cancellationToken)
            => (await _budgetService.RemoveAsync(id, query, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
