using System.Net;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.Application.Users.RequestPasswordReset;

internal static class PasswordResetEmail
{
    public static EmailMessage Create(User user, string link)
    {
        var minutes = (int)PasswordResetToken.Lifetime.TotalMinutes;

        var text = $"""
            Cześć {user.FirstName},

            ktoś poprosił o ustawienie nowego hasła do Twojego konta w Turniejo. Aby to zrobić, otwórz link:

            {link}

            Link jest ważny {minutes} minut i działa tylko raz. Jeśli to nie Ty, zignoruj tę wiadomość - hasło się nie zmieni.

            Turniejo
            """;

        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:1.5;color:#18181b;max-width:480px">
              <p>Cześć {WebUtility.HtmlEncode(user.FirstName)},</p>
              <p>ktoś poprosił o ustawienie nowego hasła do Twojego konta w Turniejo.</p>
              <p><a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:12px 20px;border-radius:8px;background:#16a34a;color:#ffffff;text-decoration:none;font-weight:bold">Ustaw nowe hasło</a></p>
              <p style="font-size:14px;color:#52525b">Link jest ważny {minutes} minut i działa tylko raz. Jeśli to nie Ty, zignoruj tę wiadomość - hasło się nie zmieni.</p>
              <p style="font-size:14px;color:#52525b">Turniejo</p>
            </div>
            """;

        return new EmailMessage(user.Email, "Ustaw nowe hasło w Turniejo", text, html);
    }
}
