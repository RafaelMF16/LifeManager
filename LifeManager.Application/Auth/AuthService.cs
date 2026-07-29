using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LifeManager.Application.Auth
{
    public class AuthService(IConfiguration configuration, IRefreshTokenRepository refreshTokenRepository)
    {
        private const short WORK_FACTOR = 10;
        private const string ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "accessTokenSecretKey";
        private const string REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "refreshTokenSecretKey";
        private const short ACCESS_TOKEN_EXPIRATION_MINUTES = 15;
        private const short REFRESH_TOKEN_EXPIRATION_DAYS = 7;

        private readonly IConfiguration _configuration = configuration;
        private readonly IRefreshTokenRepository _refreshTokenRepository = refreshTokenRepository;

        public string EncryptPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, WORK_FACTOR);
        }

        public bool VerifyPassword(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }

        public LoginResponseDto GenerateTokens(int userId)
        {
            var accessToken = GenerateAccessToken(userId);
            var refreshToken = GenerateRefreshToken();
            var hashedRefreshToken = HashRefreshToken(refreshToken);

            SaveRefreshToken(hashedRefreshToken, userId);

            return new LoginResponseDto(accessToken, refreshToken);
        }

        private string GetAccessTokenSecretKey()
        {
            return _configuration[ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE]
                ?? throw new Exception($"Environment variable [{ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE}] not found");
        }

        private string GetRefreshTokenSecretKey()
        {
            return _configuration[REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE]
                ?? throw new Exception($"Environment variable [{REFRESH_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE}] not found");
        }

        private string GenerateAccessToken(int userId)
        {
            var secretKey = GetAccessTokenSecretKey();
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
            var secretKey = GetRefreshTokenSecretKey();
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var tokenBytes = Encoding.UTF8.GetBytes(refreshToken);

            using var hmac = new HMACSHA256(keyBytes);
            var hashBytes = hmac.ComputeHash(tokenBytes);

            return Convert.ToBase64String(hashBytes);
        }

        private void SaveRefreshToken(string token, int userId)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddDays(REFRESH_TOKEN_EXPIRATION_DAYS);
            var refreshToken = RefreshToken.Create(userId, token, expiresAt, false);

            _refreshTokenRepository.Add(refreshToken);
        }
    }
}