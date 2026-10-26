using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Auth.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(UserService userService, TokenService tokenService, RegistrationService registrationService) : Controller
    {
        private readonly UserService _userService = userService;
        private readonly TokenService _tokenService = tokenService;
        private readonly RegistrationService _registrationService = registrationService;

        /// <summary>Step 1 of the sign-up: always 202 for a valid name and e-mail, whether or not it has an account.</summary>
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto, CancellationToken cancellationToken)
            => (await _registrationService.RegisterAsync(registerDto, cancellationToken)).Match(Accepted);

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken cancellationToken)
            => (await _userService.AuthenticateUserAsync(loginDto, cancellationToken)).Match(tokens =>
            {
                RefreshTokenCookie.Append(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);

                return Ok(new { tokens.AccessToken });
            });

        [HttpPost("Refresh")]
        public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
        {
            var result = await _tokenService.RefreshTokensAsync(RefreshTokenCookie.Read(Request), cancellationToken);

            if (!result.IsSuccess)
                RefreshTokenCookie.Delete(Response);

            return result.Match(tokens =>
            {
                RefreshTokenCookie.Append(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);

                return Ok(new { tokens.AccessToken });
            });
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            var result = await _tokenService.RevokeRefreshTokenAsync(RefreshTokenCookie.Read(Request), cancellationToken);

            RefreshTokenCookie.Delete(Response);

            return result.Match(NoContent);
        }
    }
}