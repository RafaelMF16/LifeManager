using LifeManager.Domain.Auth.Errors;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Shared.Results;

namespace LifeManager.Application.Test.Auth.Mocks
{
    /// <summary>
    /// Keeps the sent messages in <see cref="Sent"/> (shared, like the repository singletons) instead of sending them.
    /// Set <see cref="FailNextSends"/> to make the provider fail.
    /// </summary>
    public class EmailSenderMock : IEmailSender
    {
        public static List<EmailMessage> Sent { get; } = [];

        public static bool FailNextSends { get; set; }

        public static void Reset()
        {
            Sent.Clear();
            FailNextSends = false;
        }

        public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (FailNextSends)
                return Task.FromResult<Result>(AuthErrors.EmailNotSent);

            Sent.Add(message);
            return Task.FromResult(Result.Success());
        }
    }
}
