using LifeManager.Domain.Shared.Results;

namespace LifeManager.Domain.Auth.Interfaces
{
    public sealed record EmailMessage(
        string ToAddress,
        string ToName,
        string Subject,
        string HtmlBody,
        string TextBody);

    /// <summary>
    /// Sends a transactional e-mail. A failure (provider down, rejected request) comes back as a failed
    /// <see cref="Result"/> and is already logged by the implementation, so callers only decide what to answer.
    /// </summary>
    public interface IEmailSender
    {
        Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken);
    }
}
