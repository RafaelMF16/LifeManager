using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Transactions.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/MonthlySummaries/{monthlySummaryId:int}/[controller]")]
    public class TransactionsController(TransactionService transactionService) : Controller
    {
        private readonly TransactionService _transactionService = transactionService;

        [HttpGet]
        public async Task<IActionResult> GetAll(int monthlySummaryId, [FromQuery] TransactionListQueryDto query, CancellationToken cancellationToken)
            => (await _transactionService.GetPagedAsync(monthlySummaryId, query, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int monthlySummaryId, int id, CancellationToken cancellationToken)
            => (await _transactionService.GetByIdAsync(monthlySummaryId, id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create(int monthlySummaryId, [FromBody] TransactionDto transactionDto, CancellationToken cancellationToken)
            => (await _transactionService.CreateAsync(monthlySummaryId, transactionDto, User.GetUserId(), cancellationToken))
                .Match(transaction => CreatedAtAction(nameof(GetById), new { monthlySummaryId, id = transaction.Id }, transaction));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int monthlySummaryId, int id, [FromBody] TransactionDto transactionDto, CancellationToken cancellationToken)
            => (await _transactionService.UpdateAsync(monthlySummaryId, id, transactionDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int monthlySummaryId, int id, CancellationToken cancellationToken)
            => (await _transactionService.DeleteAsync(monthlySummaryId, id, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
