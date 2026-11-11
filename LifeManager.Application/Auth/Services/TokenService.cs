using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.EnvironmentVariables.Services;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Shared.Results;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LifeManager.Application.Auth.Services
{
    public class TokenService(
        IRefreshTokenRepository refreshTokenRepository,
        EnvironmentVariableService environmentVariableService,
        TimeProvider timeProvider)
    {
        /// <summary>Who issues the access tokens and who they're for; the API's JWT validation requires both.</summary>
        public const string AccessTokenIssuer = "LifeManager.Api";
        public const string AccessTokenAudience = "LifeManager.Web";

        private const string ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "accessTokenSecretKey";
        private const string REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "refreshTokenSecretKey";
        private const short ACCESS_TOKEN_EXPIRATION_MINUTES = 15;
        private const short REFRESH_TOKEN_EXPIRATION_DAYS = 7;
        private const short SESSION_MAX_LIFETIME_DAYS = 30;

        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
        private readonly EnvironmentVariableService _environmentVariableService = environmentVariableService;
        private readonly TimeProvider _timeProvider = timeProvider;

        public async Task<Result<LoginResponseDto>> GenerateTokensAsync(int userId, CancellationToken cancellationToken)
        {
            var now = _timeProvider.GetUtcNow();
            var refreshToken = GenerateRefreshToken();

            var tokensResult = GenerateAccessToken(userId, now)
                .Bind(accessToken => HashRefreshToken(refreshToken)
                    .Bind(hashedRefreshToken => CreateRefreshToken(hashedRefreshToken, userId, now))
                    .Map(newToken => (AccessToken: accessToken, NewToken: newToken)));

            if (!tokensResult.IsSuccess)
                return tokensResult.Error;

            var (accessToken, newToken) = tokensResult.Value;
            await _refreshTokenRepository.ReplaceActiveTokenAsync(newToken, now, cancellationToken);

            return new LoginResponseDto(accessToken, refreshToken, newToken.ExpiresAt);
        }

        /// <summary>
        /// Rotates the refresh token: the presented token is consumed and a new pair is issued for the same session.
        /// Presenting an already revoked token is treated as reuse (possible theft) and revokes every active token of the user.
        /// </summary>
        public async Task<Result<LoginResponseDto>> RefreshTokensAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return AuthErrors.InvalidRefreshToken;

            var tokenHashResult = HashRefreshToken(refreshToken).Bind(RefreshTokenHash.Create);
            if (!tokenHashResult.IsSuccess)
                return tokenHashResult.Error;

            var currentToken = await _refreshTokenRepository.GetByHashAsync(tokenHashResult.Value, cancellationToken);
            if (currentToken is null)
                return AuthErrors.InvalidRefreshToken;

            if (currentToken.IsRevoked)
            {
                await _refreshTokenRepository.RevokeAllActiveByUserIdAsync(currentToken.UserId, cancellationToken);
                return AuthErrors.InvalidRefreshToken;
            }

            var now = _timeProvider.GetUtcNow();
            if (!currentToken.IsActive(now))
                return AuthErrors.InvalidRefreshToken;

            if (!await _refreshTokenRepository.TryConsumeAsync(currentToken.TokenHash, now, cancellationToken))
                return AuthErrors.InvalidRefreshToken;

            var userId = currentToken.UserId.Value;
            var newRefreshToken = GenerateRefreshToken();

            var rotationResult = GenerateAccessToken(userId, now)
                .Bind(accessToken => HashRefreshToken(newRefreshToken)
                    .Bind(hashedRefreshToken => currentToken.Rotate(hashedRefreshToken, now, TimeSpan.FromDays(REFRESH_TOKEN_EXPIRATION_DAYS)))
                    .Map(rotatedToken => (AccessToken: accessToken, RotatedToken: rotatedToken)));

            if (!rotationResult.IsSuccess)
                return rotationResult.Error;

            var (newAccessToken, rotatedToken) = rotationResult.Value;
            await _refreshTokenRepository.AddAsync(rotatedToken, cancellationToken);

            return new LoginResponseDto(newAccessToken, newRefreshToken, rotatedToken.ExpiresAt);
        }

        public async Task<Result> RevokeRefreshTokenAsync(string? refreshToken, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return Result.Success();

            var tokenHashResult = HashRefreshToken(refreshToken).Bind(RefreshTokenHash.Create);
            if (!tokenHashResult.IsSuccess)
                return tokenHashResult.Error;

            await _refreshTokenRepository.RevokeByHashAsync(tokenHashResult.Value, cancellationToken);

            return Result.Success();
        }

        /// <summary>The stored form of a refresh token (the cookie's), to find its session.</summary>
        public Result<RefreshTokenHash> HashOf(string refreshToken)
            => HashRefreshToken(refreshToken).Bind(RefreshTokenHash.Create);

        private Result<string> GenerateAccessToken(int userId, DateTimeOffset now)
        {
            return _environmentVariableService.GetEnvironmentVariable(ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE)
                .Map(secretKey =>
                {
                    var claims = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId.ToString())]);
                    var encodedSecretKey = Encoding.ASCII.GetBytes(secretKey);
                    var tokenConfig = new SecurityTokenDescriptor
                    {
                        Subject = claims,
                        Issuer = AccessTokenIssuer,
                        Audience = AccessTokenAudience,
                        // All three from the same clock: the handler would otherwise stamp IssuedAt and NotBefore with the system time.
                        IssuedAt = now.UtcDateTime,
                        NotBefore = now.UtcDateTime,
                        Expires = now.UtcDateTime.AddMinutes(ACCESS_TOKEN_EXPIRATION_MINUTES),
                        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(encodedSecretKey), SecurityAlgorithms.HmacSha256Signature)
                    };

                    var tokenHandler = new JwtSecurityTokenHandler();
                    var token = tokenHandler.CreateToken(tokenConfig);

                    return tokenHandler.WriteToken(token);
                });
        }

        private static string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString();
        }

        private Result<string> HashRefreshToken(string refreshToken)
        {
            return _environmentVariableService.GetEnvironmentVariable(REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE)
                .Map(secretKey =>
                {
                    var keyBytes = Encoding.UTF8.GetBytes(secretKey);
                    var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);

                    using var hmac = new HMACSHA256(keyBytes);
                    var hashBytes = hmac.ComputeHash(tokenBytes);

                    return Convert.ToBase64String(hashBytes);
                });
        }

        private static Result<RefreshToken> CreateRefreshToken(string token, int userId, DateTimeOffset now)
        {
            var expiresAt = now.AddDays(REFRESH_TOKEN_EXPIRATION_DAYS);
            var sessionExpiresAt = now.AddDays(SESSION_MAX_LIFETIME_DAYS);
            return RefreshToken.Create(userId, token, now, expiresAt, sessionExpiresAt, false);
        }
    }
}
