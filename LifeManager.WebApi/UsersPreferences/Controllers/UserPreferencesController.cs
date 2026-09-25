using LifeManager.Application.UsersPreferences.DTOs;
using LifeManager.Application.UsersPreferences.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.UsersPreferences.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserPreferencesController(UserPreferencesService userPreferencesService) : Controller
    {
        private readonly UserPreferencesService _userPreferencesService = userPreferencesService;

        [Authorize]
        [HttpGet]
        public IActionResult GetUserPreferencesByUserId()
        {
            var userPreferences = _userPreferencesService.GetUserPreferencesByUserId(User.GetUserId());
            return Ok(userPreferences);
        }

        [Authorize]
        [HttpPut]
        public IActionResult AddOrUpdate(UserPreferencesDto userPreferencesDto)
            => _userPreferencesService.AddOrUpdate(userPreferencesDto, User.GetUserId()).Match(userPreferences => Ok(userPreferences));
    }
}
