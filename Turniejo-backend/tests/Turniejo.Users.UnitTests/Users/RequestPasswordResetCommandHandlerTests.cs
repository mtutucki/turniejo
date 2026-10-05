using FluentAssertions;
using Microsoft.Extensions.Logging;
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

    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IUsersDbContext _dbContext;
    private readonly RecordingLogger<RequestPasswordResetCommandHandler> _logger;
    private readonly RequestPasswordResetCommandHandler _handler;

    public RequestPasswordResetCommandHandlerTests()
    {
        _keycloakAdminService = Substitute.For<IKeycloakAdminService>();
        _dbContext = Substitute.For<IUsersDbContext>();
        _logger = new RecordingLogger<RequestPasswordResetCommandHandler>();
        _handler = new RequestPasswordResetCommandHandler(_keycloakAdminService, _dbContext, _logger);
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
        await _keycloakAdminService.DidNotReceive().SendPasswordResetEmailAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSendResetEmail_WhenUserExists()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        var command = new RequestPasswordResetCommand(user.Email);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _keycloakAdminService.Received(1).SendPasswordResetEmailAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenKeycloakThrows()
    {
        // Arrange — błąd Keycloak nie powinien wyciekać do usera
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        _keycloakAdminService.SendPasswordResetEmailAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("Keycloak unavailable")));
        var command = new RequestPasswordResetCommand(user.Email);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldLogError_WhenKeycloakThrows()
    {
        // Zamyka lukę z audytu: komentarz w kodzie deklarował logowanie błędu, ale wywołania loggera
        // nie było — awaria Keycloaka (np. padnięty SMTP) była całkowicie niewidoczna.
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        var keycloakException = new Exception("Keycloak unavailable");
        _keycloakAdminService.SendPasswordResetEmailAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(keycloakException));
        var command = new RequestPasswordResetCommand(user.Email);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Exception == keycloakException);
    }

    [Fact]
    public async Task Handle_ShouldNotLog_WhenKeycloakSucceeds()
    {
        var user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski").Value;
        _dbContext.GetUserByEmailAsync(user.Email, Arg.Any<CancellationToken>())
            .Returns(user);
        var command = new RequestPasswordResetCommand(user.Email);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _logger.Entries.Should().BeEmpty();
    }
}