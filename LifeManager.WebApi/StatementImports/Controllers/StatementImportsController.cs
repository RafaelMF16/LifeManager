using LifeManager.Application.StatementImports.DTOs;
using LifeManager.Application.StatementImports.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.StatementImports.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class StatementImportsController(StatementImportService statementImportService, StatementImportRowService statementImportRowService) : Controller
    {
        private readonly StatementImportService _statementImportService = statementImportService;
        private readonly StatementImportRowService _statementImportRowService = statementImportRowService;

        /// <summary>The user's imports: drafts first, then newest first.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] StatementImportListQueryDto query, CancellationToken cancellationToken)
            => (await _statementImportService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>The import, and while it is a draft its months with counts and totals.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _statementImportService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Creates a draft from the lines read from a bank file, with categories suggested and duplicates left out.</summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] StatementImportDto statementImportDto, CancellationToken cancellationToken)
            => (await _statementImportService.CreateAsync(statementImportDto, User.GetUserId(), cancellationToken))
                .Match(statementImport => CreatedAtAction(nameof(GetById), new { id = statementImport.Id }, statementImport));

        /// <summary>Posts the included rows as transactions, opening the missing months of any year.</summary>
        [HttpPost("{id:int}/Commit")]
        public async Task<IActionResult> Commit(int id, CancellationToken cancellationToken)
            => (await _statementImportService.CommitAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Discards a draft, or removes a confirmed import from the history (its transactions stay).</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _statementImportService.DeleteAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);

        [HttpGet("{id:int}/Rows")]
        public async Task<IActionResult> GetRows(int id, [FromQuery] StatementImportRowListQueryDto query, CancellationToken cancellationToken)
            => (await _statementImportRowService.GetPagedAsync(id, query, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPut("{id:int}/Rows/{rowId:int}")]
        public async Task<IActionResult> UpdateRow(int id, int rowId, [FromBody] StatementImportRowDto rowDto, CancellationToken cancellationToken)
            => (await _statementImportRowService.UpdateAsync(id, rowId, rowDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Rows/Bulk")]
        public async Task<IActionResult> BulkUpdateRows(int id, [FromBody] StatementImportBulkUpdateDto bulkDto, CancellationToken cancellationToken)
            => (await _statementImportRowService.BulkUpdateAsync(id, bulkDto, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Files the other rows of the same merchant like this one (rows edited directly keep their own decision).</summary>
        [HttpPost("{id:int}/Rows/{rowId:int}/ApplyToSimilar")]
        public async Task<IActionResult> ApplyToSimilar(int id, int rowId, CancellationToken cancellationToken)
            => (await _statementImportRowService.ApplyToSimilarAsync(id, rowId, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
