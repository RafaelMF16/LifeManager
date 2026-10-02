using LifeManager.Application.UsersPreferences.DTOs;
using LifeManager.Application.UsersPreferences.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.UsersPreferences.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UserPreferencesController(UserPreferencesService userPreferencesService) : Controller
    {
        private readonly UserPreferencesService _userPreferencesService = userPreferencesService;

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(await _userPreferencesService.GetByUserIdAsync(User.GetUserId(), cancellationToken));

        [HttpPut]
        public async Task<IActionResult> AddOrUpdate([FromBody] UserPreferencesDto userPreferencesDto, CancellationToken cancellationToken)
            => (await _userPreferencesService.AddOrUpdateAsync(userPreferencesDto, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
