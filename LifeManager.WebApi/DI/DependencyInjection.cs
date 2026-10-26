using LifeManager.Application.Auth.Emails;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Infrastructure.Emails;
using LifeManager.WebApi.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using LifeManager.WebApi.Habits.Jobs;
using LifeManager.WebApi.RecurringTransactions.Jobs;
using LifeManager.WebApi.StatementImports.Jobs;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LifeManager.WebApi.DI
{
    public static class DependencyInjection
    {
        private const string ACCESS_TOKEN_SECRET_KEY_ENVIRONMENT_VARIABLE = "accessTokenSecretKey";
        private const string BACKGROUND_JOBS_ENABLED_KEY = "backgroundJobs:enabled";
        private const string FRONTEND_BASE_URL_KEY = "frontendBaseUrl";
        private const string BREVO_API_KEY = "brevoApiKey";
        private const string EMAIL_SENDER_ADDRESS_KEY = "emailSenderAddress";
        private const string EMAIL_SENDER_NAME_KEY = "emailSenderName";
        private const string DEFAULT_EMAIL_SENDER_NAME = "LifeManager";

        public static IServiceCollection AddApiServices(this WebApplicationBuilder builder)
        {
            ConfigureJwtAuthentication(builder);

            ConfigureCors(builder);

            ConfigureForwardedHeaders(builder);

            ConfigureJobs(builder);

            ConfigureEmail(builder);

            AuthRateLimits.Configure(builder.Services);

            return builder.Services;
        }

        /// <summary>
        /// With <c>brevoApiKey</c>, e-mails go through Brevo (and <c>emailSenderAddress</c> is required). Without it, only
        /// Development runs, with <see cref="LoggingEmailSender"/> writing the links to the console; any other environment
        /// fails at startup, like the other required keys.
        /// </summary>
        private static void ConfigureEmail(WebApplicationBuilder builder)
        {
            var frontendBaseUrl = RequiredSetting(builder, FRONTEND_BASE_URL_KEY);
            builder.Services.AddSingleton(new FrontendLinks(frontendBaseUrl));

            var brevoApiKey = builder.Configuration[BREVO_API_KEY];
            if (string.IsNullOrWhiteSpace(brevoApiKey))
            {
                if (!builder.Environment.IsDevelopment())
                    throw new InvalidOperationException($"Environment variable [{BREVO_API_KEY}] not found");

                builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
                return;
            }

            builder.Services.AddSingleton(new BrevoSettings(
                brevoApiKey,
                RequiredSetting(builder, EMAIL_SENDER_ADDRESS_KEY),
                builder.Configuration[EMAIL_SENDER_NAME_KEY] is { Length: > 0 } senderName ? senderName : DEFAULT_EMAIL_SENDER_NAME));

            builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>(client =>
            {
                client.BaseAddress = BrevoEmailSender.BaseAddress;
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }

        private static string RequiredSetting(WebApplicationBuilder builder, string key)
            => builder.Configuration[key] is { Length: > 0 } value
                ? value
                : throw new InvalidOperationException($"Environment variable [{key}] not found");

        /// <summary>
        /// The runners are always registered (the <c>--run-jobs</c> mode uses them). The hourly hosted services only when
        /// <c>backgroundJobs:enabled</c> isn't <c>false</c>: Cloud Run scales the API to zero, so there they're replaced by a
        /// Cloud Run Job triggered by Cloud Scheduler.
        /// </summary>
        private static void ConfigureJobs(WebApplicationBuilder builder)
        {
            builder.Services.AddSingleton<RecurringTransactionsRunner>();
            builder.Services.AddSingleton<HabitsDayCloseRunner>();
            builder.Services.AddSingleton<StatementImportsCleanupRunner>();

            if (!builder.Configuration.GetValue(BACKGROUND_JOBS_ENABLED_KEY, defaultValue: true))
                return;

            builder.Services.AddHostedService<RecurringTransactionsJob>();
            builder.Services.AddHostedService<HabitsDayCloseJob>();
            builder.Services.AddHostedService<StatementImportsCleanupJob>();
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