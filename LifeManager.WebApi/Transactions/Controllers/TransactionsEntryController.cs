using System.Globalization;
using LifeManager.Application.Transactions.DTOs;
using LifeManager.Application.Transactions.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;

namespace LifeManager.WebApi.Transactions.Controllers
{
    /// <summary>
    /// The transaction routes that aren't inside one month: <c>POST /api/Transactions</c>, a transaction entered without
    /// picking its month first, which falls into (and may open) the month of its date; and <c>GET Export</c>, the CSV of a
    /// period. Everything else about a transaction lives under its month (<see cref="TransactionsController"/>).
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/Transactions")]
    public class TransactionsEntryController(TransactionService transactionService, TransactionExportService transactionExportService) : Controller
    {
        /// <summary>How many transactions the exported file has, sent before it so the frontend can tell an empty period.</summary>
        public const string RowCountHeader = "X-Row-Count";

        private readonly TransactionService _transactionService = transactionService;
        private readonly TransactionExportService _transactionExportService = transactionExportService;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TransactionDto transactionDto, CancellationToken cancellationToken)
            => (await _transactionService.CreateQuickAsync(transactionDto, User.GetUserId(), cancellationToken))
                .Match(transaction => CreatedAtAction(
                    nameof(TransactionsController.GetById),
                    "Transactions",
                    new { monthlySummaryId = transaction.MonthlySummaryId, id = transaction.Id },
                    transaction));

        /// <summary>
        /// Downloads the period's transactions as CSV, streamed straight to the response. The period is checked (400) and
        /// the rows counted before the first byte; once streaming started, a failure aborts the connection instead.
        /// </summary>
        [HttpGet("Export")]
        [EnableRateLimiting(TransactionRateLimits.Export)]
        public async Task<IActionResult> Export([FromQuery] TransactionExportQueryDto query, CancellationToken cancellationToken)
        {
            var prepared = await _transactionExportService.PrepareAsync(User.GetUserId(), query, cancellationToken);
            if (!prepared.IsSuccess)
                return prepared.Match(_ => new EmptyResult());

            var export = prepared.Value!;
            Response.ContentType = "text/csv; charset=utf-8";
            Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = export.FileName }.ToString();
            Response.Headers[RowCountHeader] = export.RowCount.ToString(CultureInfo.InvariantCulture);

            await _transactionExportService.WriteAsync(export, Response.Body, cancellationToken);

            // The body is already written: nothing else to send.
            return new EmptyResult();
        }
    }
}
