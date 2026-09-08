using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Notifications.GetMyNotifications;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.Notifications;

public sealed class GetMyNotificationsQueryHandlerTests
{
    private readonly INotificationsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly GetMyNotificationsQueryHandler _handler;

    public GetMyNotificationsQueryHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _handler = new GetMyNotificationsQueryHandler(_dbContext, _currentUserContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyCurrentUsersNotifications_NewestFirst()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);

        var notificationOld = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.MatchStarted, "Stare", "Wiadomość", null).Value;
        var notificationNew = Notification.Create(Guid.NewGuid(), Guid.NewGuid(), NotificationType.MatchFinished, "Nowe", "Wiadomość", null).Value;

        var recipientOld = NotificationRecipient.Create(Guid.NewGuid(), notificationOld.Id, userId).Value;
        var recipientNew = NotificationRecipient.Create(Guid.NewGuid(), notificationNew.Id, userId).Value;

        _dbContext.GetNotificationRecipientsByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns([recipientOld, recipientNew]);
        _dbContext.GetNotificationByIdAsync(notificationOld.Id, Arg.Any<CancellationToken>()).Returns(notificationOld);
        _dbContext.GetNotificationByIdAsync(notificationNew.Id, Arg.Any<CancellationToken>()).Returns(notificationNew);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenUserHasNoNotifications()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        _dbContext.GetNotificationRecipientsByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns([]);

        // Act
        var result = await _handler.Handle(new GetMyNotificationsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
