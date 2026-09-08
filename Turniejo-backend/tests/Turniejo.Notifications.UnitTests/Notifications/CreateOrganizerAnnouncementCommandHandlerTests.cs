using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Notifications.CreateOrganizerAnnouncement;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.Notifications;

public sealed class CreateOrganizerAnnouncementCommandHandlerTests
{
    private readonly INotificationsDbContext _dbContext;
    private readonly IUserModule _userModule;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly CreateOrganizerAnnouncementCommandHandler _handler;

    public CreateOrganizerAnnouncementCommandHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _userModule = Substitute.For<IUserModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _currentUserContext.UserId.Returns(Guid.NewGuid());

        var publisher = new NotificationPublisher(_dbContext);
        _handler = new CreateOrganizerAnnouncementCommandHandler(_dbContext, publisher, _userModule, _currentUserContext);
    }

    [Fact]
    public async Task Handle_ShouldCreateNotification_AndOneRecipientPerScopedUser()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>()).Returns(userIds);

        var command = new CreateOrganizerAnnouncementCommand(tournamentId, "Ważny komunikat", "Trening przełożony.");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RecipientCount.Should().Be(3);
        await _dbContext.Received(1).AddNotificationAsync(Arg.Any<Turniejo.Notifications.Domain.Entities.Notification>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).AddNotificationRecipientsAsync(
            Arg.Is<IReadOnlyCollection<Turniejo.Notifications.Domain.Entities.NotificationRecipient>>(r => r.Count == 3), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldStillCreateNotification_WhenNoScopedUsersFound()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var command = new CreateOrganizerAnnouncementCommand(tournamentId, "Tytuł", "Treść");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RecipientCount.Should().Be(0);
    }
}
