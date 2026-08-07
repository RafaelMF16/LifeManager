using LifeManager.Application.Auth.DTOs;
using LifeManager.Application.EnvironmentVariables.Services;
using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Shared.Results;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LifeManager.Application.Auth.Services
{
    public class TokenService(IRefreshTokenRepository refreshTokenRepository, EnvironmentVariableService environmentVariableService)
    {
        private const string ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "accessTokenSecretKey";
        private const string REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "refreshTokenSecretKey";
        private const short ACCESS_TOKEN_EXPIRATION_MINUTES = 15;
        private const short REFRESH_TOKEN_EXPIRATION_DAYS = 7;

        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;
        private readonly EnvironmentVariableService _environmentVariableService = environmentVariableService;

        public Result<LoginResponseDto> GenerateTokens(int userId)
        {
            RevokeActiveToken(userId);

            var accessToken = GenerateAccessToken(userId);
            var refreshToken = GenerateRefreshToken();
            var hashedRefreshToken = HashRefreshToken(refreshToken);

            return SaveRefreshToken(hashedRefreshToken, userId)
                .Map(_ => new LoginResponseDto(accessToken, refreshToken));
        }

        private string GenerateAccessToken(int userId)
        {
            var secretKey = _environmentVariableService.GetEnvironmentVariable(ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE);
            var claims = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, userId.ToString())]);
            var encodedSecretKey = Encoding.ASCII.GetBytes(secretKey);
            var tokenConfig = new SecurityTokenDescriptor
            {
                Subject = claims,
                Expires = DateTime.UtcNow.AddMinutes(ACCESS_TOKEN_EXPIRATION_MINUTES),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(encodedSecretKey), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenConfig);

            return tokenHandler.WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            return Guid.NewGuid().ToString();
        }

        private string HashRefreshToken(string refreshToken)
        {
            var secretKey = _environmentVariableService.GetEnvironmentVariable(REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE);
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(tokenBytes);

            return Convert.ToBase64String(hashBytes);
        }

        private Result<RefreshToken> SaveRefreshToken(string token, int userId)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddDays(REFRESH_TOKEN_EXPIRATION_DAYS);
            return RefreshToken.Create(userId, token, expiresAt, false)
                .Tap(refreshToken => _refreshTokenRepository.Add(refreshToken));
        }

        private void RevokeActiveToken(int userId)
        {
            var activeToken = _refreshTokenRepository.GetValidTokenByUserId(userId);
            if (activeToken is null)
                return;

            activeToken.RevokeToken();
            _refreshTokenRepository.UpdateRevoked(activeToken);
        }
    }
}
