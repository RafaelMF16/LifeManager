using LifeManager.Application.Categories.DTOs;
using LifeManager.Application.Categories.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Categories.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class CategoriesController(CategoryService categoryService) : Controller
    {
        private readonly CategoryService _categoryService = categoryService;

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
            => Ok(await _categoryService.GetAllAsync(User.GetUserId(), cancellationToken));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _categoryService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CategoryDto categoryDto, CancellationToken cancellationToken)
            => (await _categoryService.CreateAsync(categoryDto, User.GetUserId(), cancellationToken))
                .Match(category => CreatedAtAction(nameof(GetById), new { id = category.Id }, category));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] CategoryDto categoryDto, CancellationToken cancellationToken)
            => (await _categoryService.UpdateAsync(id, categoryDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _categoryService.DeleteAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
