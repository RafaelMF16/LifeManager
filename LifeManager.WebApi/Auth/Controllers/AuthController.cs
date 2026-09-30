using LifeManager.Application.Auth.Services;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Auth.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(UserService userService, TokenService tokenService) : Controller
    {
        private readonly UserService _userService = userService;
        private readonly TokenService _tokenService = tokenService;

        [HttpPost("Register")]
        public IActionResult Register([FromBody] UserDto userDto)
            => _userService.AddUser(userDto).Match(_ => Created());

        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginDto loginDto)
            => _userService.AuthenticateUser(loginDto).Match(tokens =>
            {
                RefreshTokenCookie.Append(Response, tokens.RefreshToken);

                return Ok(new { tokens.AccessToken });
            });

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var result = await _tokenService.RevokeRefreshTokenAsync(RefreshTokenCookie.Read(Request), cancellationToken);

            RefreshTokenCookie.Delete(Response);

            return result.Match(NoContent);
        }
    }
}