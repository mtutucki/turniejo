using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.RequestPasswordReset;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class RequestPasswordResetCommandHandlerTests
{
    // NSubstitute nie potrafi sensownie zweryfikować generycznego ILogger.Log<TState> (LogError to
    // extension method wołający Log<FormattedLogValues>, nieznanego z zewnątrz) — prościej i pewniej
    // jest podstawić ręcznie napisaną, nagrywającą implementację ILogger niż walczyć z dopasowywaniem
    // generycznych wywołań.
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }

    private const string LinkPrefix = "https://turniejo.test/password-reset/confirm?token=";

    private readonly IEmailSender _emailSender;
    private readonly IUsersDbContext _dbContext;
    private readonly RecordingLogger<RequestPasswordResetCommandHandler> _logger;
    private readonly RequestPasswordResetCommandHandler _handler;

    public RequestPasswordResetCommandHandlerTests()
    {
        _emailSender = Substitute.For<IEmailSender>();
        _dbContext = Substitute.For<IUsersDbContext>();
        _dbContext.GetPasswordResetTokensAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _logger = new RecordingLogger<RequestPasswordResetCommandHandler>();
        // Ukośnik na końcu jak w typowej konfiguracji — link nie może mieć podwójnego "//".
        var appOptions = Options.Create(new AppOptions { PublicUrl = "https://turniejo.test/" });
        _handler = new RequestPasswordResetCommandHandler(_emailSender, _dbContext, appOptions, _logger);
    }

    private User SeedUser()
    {
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        return user;
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenUserDoesNotExist()
    {
        // Arrange — celowo nie ujawniamy że email nie istnieje
        _dbContext.GetUserByEmailAsync("nieznany@turniejo.pl", Arg.Any<CancellationToken>())
            .Returns((User?)null);
        var command = new RequestPasswordResetCommand("nieznany@turniejo.pl");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().AddPasswordResetTokenAsync(Arg.Any<PasswordResetToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotSendEmail_WhenAccountIsDeleted()
    {
        var user = SeedUser();
        user.Anonymize();
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await _handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldEmailLinkToApp_AndStoreOnlyTokenHash_WhenUserExists()
    {
        // Arrange
        var user = SeedUser();
        PasswordResetToken? stored = null;
        EmailMessage? sent = null;
        await _dbContext.AddPasswordResetTokenAsync(Arg.Do<PasswordResetToken>(t => stored = t), Arg.Any<CancellationToken>());
        await _emailSender.SendAsync(Arg.Do<EmailMessage>(m => sent = m), Arg.Any<CancellationToken>());

        // Act
        var result = await _handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        sent.Should().NotBeNull();
        sent!.To.Should().Be(user.Email);
        sent.TextBody.Should().Contain(LinkPrefix);
        sent.HtmlBody.Should().Contain(LinkPrefix);

        var token = sent.TextBody[(sent.TextBody.IndexOf(LinkPrefix, StringComparison.Ordinal) + LinkPrefix.Length)..]
            .Split('\r', '\n')[0];
        stored.Should().NotBeNull();
        stored!.UserId.Should().Be(user.Id);
        stored.TokenHash.Should().Be(PasswordResetTokenGenerator.Hash(token));
        stored.TokenHash.Should().NotBe(token);
        stored.IsUsable(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldRemovePreviousTokens_WhenUserRequestsAgain()
    {
        var user = SeedUser();
        var previous = PasswordResetToken.Create(user.Id, "old-hash");
        _dbContext.GetPasswordResetTokensAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns([previous]);

        await _handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        _dbContext.Received(1).RemovePasswordResetTokens(Arg.Is<IEnumerable<PasswordResetToken>>(t => t.Contains(previous)));
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenEmailSenderThrows()
    {
        // Arrange — błąd wysyłki nie powinien wyciekać do usera
        var user = SeedUser();
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("SMTP unavailable")));

        // Act
        var result = await _handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldLogError_WhenEmailSenderThrows()
    {
        // Zamyka lukę z audytu: komentarz w kodzie deklarował logowanie błędu, ale wywołania loggera
        // nie było — awaria wysyłki (np. padnięty SMTP) była całkowicie niewidoczna.
        var user = SeedUser();
        var smtpException = new Exception("SMTP unavailable");
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(smtpException));

        // Act
        await _handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        // Assert
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == smtpException);
    }

    [Fact]
    public async Task Handle_ShouldNotLog_WhenEmailIsSent()
    {
        var user = SeedUser();

        // Act
        await _handler.Handle(new RequestPasswordResetCommand(user.Email), CancellationToken.None);

        // Assert
        _logger.Entries.Should().BeEmpty();
    }
}
