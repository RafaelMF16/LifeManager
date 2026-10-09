using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using LifeManager.WebApi.Habits.Jobs;
using LifeManager.WebApi.RecurringTransactions.Jobs;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LifeManager.WebApi.DI
{
    public static class DependencyInjection
    {
        private const string ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "accessTokenSecretKey";
        private const string BACKGROUND_JOBS_ENABLED_KEY = "backgroundJobs:enabled";

        public static IServiceCollection AddApiServices(this WebApplicationBuilder builder)
        {
            ConfigureJwtAuthentication(builder);

            ConfigureCors(builder);

            ConfigureForwardedHeaders(builder);

            ConfigureJobs(builder);

            return builder.Services;
        }

        /// <summary>
        /// The runners are always registered (the <c>--run-jobs</c> mode uses them). The hourly hosted services only when
        /// <c>backgroundJobs:enabled</c> isn't <c>false</c>: Cloud Run scales the API to zero, so there they're replaced by a
        /// Cloud Run Job triggered by Cloud Scheduler.
        /// </summary>
        private static void ConfigureJobs(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<RecurringTransactionsRunner>();
            builder.Services.AddSingleton<HabitsDayCloseRunner>();

            if (!builder.Configuration.GetValue(BACKGROUND_JOBS_ENABLED_KEY, defaultValue: true))
                return;

            builder.Services.AddHostedService<RecurringTransactionsJob>();
            builder.Services.AddHostedService<HabitsDayCloseJob>();
        }

        /// <summary>
        /// Cloud Run (and Firebase Hosting in front of it) terminates TLS and forwards plain HTTP, so the scheme comes from
        /// <c>X-Forwarded-Proto</c>. The proxies' addresses aren't known in advance, hence no KnownNetworks/KnownProxies.
        /// </summary>
        private static void ConfigureForwardedHeaders(WebApplicationBuilder builder)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });
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
                options.AddPolicy("AllowFrontend", policy => policy
                    .WithOrigins("https://localhost:5173")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });
        }
    }
}