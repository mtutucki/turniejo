using System.Collections.Concurrent;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.IntegrationTests.Infrastructure;

// Zamiast SMTP — testy czytają stąd wysłane maile (np. żeby wyjąć link z tokenem resetu hasła).
public sealed class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Reset() => Sent.Clear();
}
