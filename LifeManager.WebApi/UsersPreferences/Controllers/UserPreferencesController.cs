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
        public IActionResult Get()
            => Ok(_userPreferencesService.GetUserPreferencesByUserId(User.GetUserId()));

        [HttpPut]
        public IActionResult AddOrUpdate([FromBody] UserPreferencesDto userPreferencesDto)
            => _userPreferencesService.AddOrUpdate(userPreferencesDto, User.GetUserId()).Match(Ok);
    }
}
