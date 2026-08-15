using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LifeManager.WebApi.DI
{
    public static class DependencyInjection
    {
        private const string ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "accessTokenSecretKey";

        public static IServiceCollection AddApiServices(this WebApplicationBuilder builder)
        {
            ConfigureJwtAuthentication(builder);

            ConfigureCors(builder);

            return builder.Services;
        }

        private static void ConfigureJwtAuthentication(WebApplicationBuilder builder)
        {
            var accessTokenSecretKey = builder.Configuration[ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE]
                ?? throw new InvalidOperationException($"Environment variable [{ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE}] not found");

            var encodedSecretKey = Encoding.ASCII.GetBytes(accessTokenSecretKey);
            builder.Services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x =>
            {
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(encodedSecretKey),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                };
            });
        }

        private static void ConfigureCors(WebApplicationBuilder builder)
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy
                    => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });
        }
    }
}