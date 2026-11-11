using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Transactions.Controllers
{
    /// <summary>
    /// <c>POST /api/Transactions</c>: a transaction entered without picking its month first, which falls into (and may
    /// open) the month of its date. Everything else about a transaction lives under its month
    /// (<see cref="TransactionsController"/>).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/Transactions")]
    public class TransactionsEntryController(TransactionService transactionService) : Controller
    {
        private readonly TransactionService _transactionService = transactionService;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TransactionDto transactionDto, CancellationToken cancellationToken)
            => (await _transactionService.CreateQuickAsync(transactionDto, User.GetUserId(), cancellationToken))
                .Match(transaction => CreatedAtAction(
                    nameof(TransactionsController.GetById),
                    "Transactions",
                    new { monthlySummaryId = transaction.MonthlySummaryId, id = transaction.Id },
                    transaction));
    }
}
