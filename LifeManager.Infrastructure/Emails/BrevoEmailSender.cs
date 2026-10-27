using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Shared.Results;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LifeManager.Infrastructure.Emails
{
    public sealed record BrevoSettings(string ApiKey, string SenderAddress, string SenderName);

    /// <summary>
    /// Sends through Brevo's transactional API (<c>POST /v3/smtp/email</c>). The <see cref="HttpClient"/> comes from
    /// <c>AddHttpClient</c> in the WebApi, already pointing at <see cref="BaseAddress"/> with the timeout set.
    /// </summary>
    public sealed class BrevoEmailSender(HttpClient httpClient, BrevoSettings settings, ILogger<BrevoEmailSender> logger) : IEmailSender
    {
        public static readonly Uri BaseAddress = new("https://api.brevo.com/v3/");

        // Brevo rejects an empty recipient name, so a missing one is left out of the request.
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient _httpClient = httpClient;
        private readonly BrevoSettings _settings = settings;
        private readonly ILogger<BrevoEmailSender> _logger = logger;

        public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
            {
                Content = JsonContent.Create(new
                {
                    sender = new { name = _settings.SenderName, email = _settings.SenderAddress },
                    to = new[] { new { email = message.ToAddress, name = message.ToName } },
                    subject = message.Subject,
                    htmlContent = message.HtmlBody,
                    textContent = message.TextBody
                }, options: SerializerOptions)
            };
            request.Headers.Add("api-key", _settings.ApiKey);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return Result.Success();

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Brevo rejected the e-mail \"{Subject}\": {StatusCode} {Body}", message.Subject, (int)response.StatusCode, body);
            }
            catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not reach Brevo to send the e-mail \"{Subject}\"", message.Subject);
            }

            return AuthErrors.EmailNotSent;
        }
    }
}
