using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Auth.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(UserService userService) : Controller
    {
        private readonly UserService _userService = userService;

        [HttpPost("Register")]
        public IActionResult Register([FromBody] UserDto userDto)
            => _userService.AddUser(userDto).Match(_ => Created());

        [HttpPost("Login")]
        public IActionResult Login([FromBody] LoginDto loginDto)
            => _userService.AuthenticateUser(loginDto).Match(tokens =>
            {
                Response.Cookies.Append("refreshToken", tokens.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(7),
                    Path = "/api/Auth"
                });

                return Ok(new { tokens.AccessToken });
            });
    }
}