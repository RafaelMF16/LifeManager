using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.WebApi.Auth;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeManager.WebApi.Users.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class UsersController(UserService userService, UserAccountService userAccountService) : Controller
    {
        private readonly UserAccountService _userAccountService = userAccountService;
        private readonly UserService _userService = userService;

        [HttpGet("Me")]
        public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
            => (await _userService.GetCurrentUserAsync(User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPut("Me")]
        public async Task<IActionResult> Rename([FromBody] UpdateCurrentUserDto updateCurrentUserDto, CancellationToken cancellationToken)
            => (await _userService.RenameAsync(User.GetUserId(), updateCurrentUserDto, cancellationToken)).Match(Ok);

        /// <summary>Schedules the account's deletion and signs this browser out (every other session ends too).</summary>
        [HttpPost("Me/Deletion")]
        [EnableRateLimiting(AccountRateLimits.Sensitive)]
        public async Task<IActionResult> RequestDeletion([FromBody] RequestAccountDeletionDto requestAccountDeletionDto, CancellationToken cancellationToken)
            => (await _userAccountService.RequestDeletionAsync(User.GetUserId(), requestAccountDeletionDto, cancellationToken)).Match(deletion =>
            {
                RefreshTokenCookie.Delete(Response);

                return Accepted(deletion);
            });
    }
}
