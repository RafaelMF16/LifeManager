using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.Users.DTOs;
using LifeManager.Application.Users.Services;
using LifeManager.WebApi.Extensions;
using LifeManager.WebApi.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeManager.WebApi.Auth.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        UserService userService,
        TokenService tokenService,
        RegistrationService registrationService,
        PasswordResetService passwordResetService,
        UserAccountService userAccountService) : Controller
    {
        private readonly UserAccountService _userAccountService = userAccountService;
        private readonly UserService _userService = userService;
        private readonly TokenService _tokenService = tokenService;
        private readonly RegistrationService _registrationService = registrationService;
        private readonly PasswordResetService _passwordResetService = passwordResetService;

        /// <summary>Step 1 of the sign-up: always 202 for a valid name and e-mail, whether or not it has an account.</summary>
        [HttpPost("Register")]
        [EnableRateLimiting(AuthRateLimits.EmailRequests)]
        public async Task<IActionResult> Register([FromBody] RegisterDto registerDto, CancellationToken cancellationToken)
            => (await _registrationService.RegisterAsync(registerDto, cancellationToken)).Match(Accepted);

        [HttpPost("Register/Verify")]
        [EnableRateLimiting(AuthRateLimits.EmailTokens)]
        public async Task<IActionResult> VerifyRegistration([FromBody] EmailTokenDto emailTokenDto, CancellationToken cancellationToken)
            => (await _registrationService.VerifyAsync(emailTokenDto, cancellationToken)).Match(Ok);

        /// <summary>Step 2 of the sign-up: the password, sent from the page the confirmation link opens.</summary>
        [HttpPost("Register/Complete")]
        [EnableRateLimiting(AuthRateLimits.EmailTokens)]
        public async Task<IActionResult> CompleteRegistration([FromBody] CompleteRegistrationDto completeRegistrationDto, CancellationToken cancellationToken)
            => (await _registrationService.CompleteAsync(completeRegistrationDto, cancellationToken)).Match(() => Created());

        /// <summary>Always 202 for a valid e-mail, whether or not it has an account.</summary>
        [HttpPost("ForgotPassword")]
        [EnableRateLimiting(AuthRateLimits.EmailRequests)]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto forgotPasswordDto, CancellationToken cancellationToken)
            => (await _passwordResetService.RequestAsync(forgotPasswordDto, cancellationToken)).Match(Accepted);

        [HttpPost("ResetPassword/Verify")]
        [EnableRateLimiting(AuthRateLimits.EmailTokens)]
        public async Task<IActionResult> VerifyPasswordReset([FromBody] EmailTokenDto emailTokenDto, CancellationToken cancellationToken)
            => (await _passwordResetService.VerifyAsync(emailTokenDto, cancellationToken)).Match(Ok);

        /// <summary>Sets the new password and ends every open session of the user.</summary>
        [HttpPost("ResetPassword")]
        [EnableRateLimiting(AuthRateLimits.EmailTokens)]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto resetPasswordDto, CancellationToken cancellationToken)
            => (await _passwordResetService.ResetAsync(resetPasswordDto, cancellationToken)).Match(NoContent);

        [HttpPost("Login")]
        [EnableRateLimiting(AuthRateLimits.Login)]
        public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken cancellationToken)
            => (await _userService.AuthenticateUserAsync(loginDto, AuthRateLimits.ClientIp(HttpContext), cancellationToken)).Match(tokens =>
            {
                RefreshTokenCookie.Append(Response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);

                return Ok(new { tokens.AccessToken, tokens.DeletionCancelled });
            });

        [HttpPost("Refresh")]
        [EnableRateLimiting(AuthRateLimits.Refresh)]
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

        /// <summary>
        /// "My account": here and not under <c>/api/Users</c> because only this path receives the session cookie, the one
        /// session that stays signed in.
        /// </summary>
        [HttpPost("ChangePassword")]
        [Authorize]
        [EnableRateLimiting(AccountRateLimits.Sensitive)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto changePasswordDto, CancellationToken cancellationToken)
            => (await _userAccountService.ChangePasswordAsync(User.GetUserId(), changePasswordDto, RefreshTokenCookie.Read(Request), cancellationToken))
                .Match(NoContent);

        /// <summary>Ends every session of the user, this one included, and deletes this browser's cookie.</summary>
        [HttpPost("LogoutAll")]
        [Authorize]
        public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
        {
            var result = await _userAccountService.LogoutAllAsync(User.GetUserId(), cancellationToken);

            RefreshTokenCookie.Delete(Response);

            return result.Match(NoContent);
        }
    }
}