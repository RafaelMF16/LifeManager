using LifeManager.Application.BudgetPlans.DTOs;
using LifeManager.Application.BudgetPlans.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.BudgetPlans.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class BudgetPlansController(BudgetPlanService budgetPlanService) : Controller
    {
        private readonly BudgetPlanService _budgetPlanService = budgetPlanService;

        /// <param name="month">"yyyy-MM".</param>
        [HttpGet("{month}")]
        public async Task<IActionResult> Get(string month, CancellationToken cancellationToken)
            => (await _budgetPlanService.GetAsync(month, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <param name="month">"yyyy-MM".</param>
        [HttpPut("{month}")]
        public async Task<IActionResult> Save(string month, [FromBody] BudgetPlanDto budgetPlanDto, CancellationToken cancellationToken)
            => (await _budgetPlanService.SaveAsync(month, budgetPlanDto, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <param name="month">"yyyy-MM".</param>
        [HttpDelete("{month}")]
        public async Task<IActionResult> Delete(string month, CancellationToken cancellationToken)
            => (await _budgetPlanService.DeleteAsync(month, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
