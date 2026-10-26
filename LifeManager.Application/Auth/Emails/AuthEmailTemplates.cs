using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Domain.UsersPreferences.Enums;
using System.Net;

namespace LifeManager.Application.Auth.Emails
{
    /// <summary>
    /// The auth e-mails in the user's language (pt-BR or en-US): a short HTML body plus a plain-text one, both with the
    /// link written out in full. Every value is HTML-encoded in the HTML body, since the name comes from the user.
    /// </summary>
    public static class AuthEmailTemplates
    {
        public static EmailMessage Confirmation(Email to, UserName name, Language language, string setPasswordUrl)
            => language == Language.EN
                ? Build(to, name, "Confirm your e-mail",
                    "Hi, {0}!",
                    "Confirm your e-mail to finish creating your LifeManager account. Open the link below and choose your password:",
                    "Choose my password", setPasswordUrl,
                    $"The link is valid for {PendingRegistration.ValidityHours} hours. If you didn't sign up, ignore this e-mail.")
                : Build(to, name, "Confirme seu e-mail",
                    "Olá, {0}!",
                    "Confirme seu e-mail para terminar de criar sua conta no LifeManager. Abra o link abaixo e escolha sua senha:",
                    "Escolher minha senha", setPasswordUrl,
                    $"O link vale por {PendingRegistration.ValidityHours} horas. Se você não se cadastrou, ignore este e-mail.");

        public static EmailMessage AccountExists(Email to, UserName name, Language language, string forgotPasswordUrl)
            => language == Language.EN
                ? Build(to, name, "You already have an account",
                    "Hi, {0}!",
                    "Someone tried to create a LifeManager account with this e-mail, but it already has one. If you forgot your password, you can choose a new one:",
                    "Reset my password", forgotPasswordUrl,
                    "If it wasn't you, ignore this e-mail: nothing changed in your account.")
                : Build(to, name, "Você já tem uma conta",
                    "Olá, {0}!",
                    "Alguém tentou criar uma conta no LifeManager com este e-mail, mas ele já tem uma. Se você esqueceu a senha, pode escolher uma nova:",
                    "Redefinir minha senha", forgotPasswordUrl,
                    "Se não foi você, ignore este e-mail: nada mudou na sua conta.");

        private static EmailMessage Build(
            Email to,
            UserName name,
            string subject,
            string greetingFormat,
            string intro,
            string buttonLabel,
            string url,
            string footer)
        {
            var text = $"""
                {string.Format(greetingFormat, name.Value)}

                {intro}

                {url}

                {footer}
                """;

            var encodedUrl = WebUtility.HtmlEncode(url);
            var html = $"""
                <!doctype html>
                <html>
                <body style="font-family: Arial, sans-serif; color: #1f2933; line-height: 1.5;">
                  <p>{WebUtility.HtmlEncode(string.Format(greetingFormat, name.Value))}</p>
                  <p>{WebUtility.HtmlEncode(intro)}</p>
                  <p><a href="{encodedUrl}" style="display: inline-block; padding: 10px 16px; background: #2563eb; color: #ffffff; text-decoration: none; border-radius: 6px;">{WebUtility.HtmlEncode(buttonLabel)}</a></p>
                  <p style="font-size: 13px; color: #52606d;">{encodedUrl}</p>
                  <p style="font-size: 13px; color: #52606d;">{WebUtility.HtmlEncode(footer)}</p>
                </body>
                </html>
                """;

            return new EmailMessage(to.Value, name.Value, subject, html, text);
        }
    }
}
