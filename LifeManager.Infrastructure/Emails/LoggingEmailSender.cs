using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Shared.Results;
using Microsoft.Extensions.Logging;

namespace LifeManager.Infrastructure.Emails
{
    /// <summary>
    /// Development only (no <c>brevoApiKey</c>): writes the e-mail to the log instead of sending it, so the links can be
    /// copied from the console. It logs the links themselves, so it must never run in production.
    /// </summary>
    public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger = logger;

        public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("E-mail to {ToAddress}: {Subject}\n{TextBody}", message.ToAddress, message.Subject, message.TextBody);

            return Task.FromResult(Result.Success());
        }
    }
}
