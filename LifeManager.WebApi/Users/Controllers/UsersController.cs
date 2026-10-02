using LifeManager.Application.Users.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Users.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UsersController(UserService userService) : Controller
    {
        private readonly UserService _userService = userService;

        [HttpGet("Me")]
        public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
            => (await _userService.GetCurrentUserAsync(User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
