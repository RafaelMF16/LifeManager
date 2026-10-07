using LifeManager.Application.RecurringTransactions.DTOs;
using LifeManager.Application.RecurringTransactions.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.RecurringTransactions.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RecurringTransactionsController(RecurringTransactionService recurringTransactionService) : Controller
    {
        private readonly RecurringTransactionService _recurringTransactionService = recurringTransactionService;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] RecurringTransactionListQueryDto query, CancellationToken cancellationToken)
            => (await _recurringTransactionService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _recurringTransactionService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RecurringTransactionDto recurringTransactionDto, CancellationToken cancellationToken)
            => (await _recurringTransactionService.CreateAsync(recurringTransactionDto, User.GetUserId(), cancellationToken))
                .Match(recurringTransaction => CreatedAtAction(nameof(GetById), new { id = recurringTransaction.Id }, recurringTransaction));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RecurringTransactionDto recurringTransactionDto, CancellationToken cancellationToken)
            => (await _recurringTransactionService.UpdateAsync(id, recurringTransactionDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Pause")]
        public async Task<IActionResult> Pause(int id, CancellationToken cancellationToken)
            => (await _recurringTransactionService.PauseAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Resume")]
        public async Task<IActionResult> Resume(int id, CancellationToken cancellationToken)
            => (await _recurringTransactionService.ResumeAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _recurringTransactionService.DeleteAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
