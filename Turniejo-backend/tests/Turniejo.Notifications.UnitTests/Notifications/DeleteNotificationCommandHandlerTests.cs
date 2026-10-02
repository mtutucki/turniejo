using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Notifications.DeleteNotification;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Notifications.UnitTests.Notifications;

public sealed class DeleteNotificationCommandHandlerTests
{
    private readonly INotificationsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly DeleteNotificationCommandHandler _handler;

    public DeleteNotificationCommandHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _handler = new DeleteNotificationCommandHandler(_dbContext, _currentUserContext);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenRecipientNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        _dbContext.GetNotificationRecipientByIdAsync(id, Arg.Any<CancellationToken>()).Returns((NotificationRecipient?)null);

        // Act
        var result = await _handler.Handle(new DeleteNotificationCommand(id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Notification.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenRecipientBelongsToAnotherUser()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        recipient.MarkAsRead();
        _dbContext.GetNotificationRecipientByIdAsync(recipient.Id, Arg.Any<CancellationToken>()).Returns(recipient);
        _currentUserContext.UserId.Returns(Guid.NewGuid());

        // Act
        var act = () => _handler.Handle(new DeleteNotificationCommand(recipient.Id), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
        _dbContext.DidNotReceive().RemoveNotificationRecipient(Arg.Any<NotificationRecipient>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNotificationIsUnread()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var recipient = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), userId).Value;
        _dbContext.GetNotificationRecipientByIdAsync(recipient.Id, Arg.Any<CancellationToken>()).Returns(recipient);
        _currentUserContext.UserId.Returns(userId);

        // Act
        var result = await _handler.Handle(new DeleteNotificationCommand(recipient.Id), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict.Notification");
        _dbContext.DidNotReceive().RemoveNotificationRecipient(Arg.Any<NotificationRecipient>());
    }

    [Fact]
    public async Task Handle_ShouldDelete_WhenReadAndOwnedByCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var recipient = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), userId).Value;
        recipient.MarkAsRead();
        _dbContext.GetNotificationRecipientByIdAsync(recipient.Id, Arg.Any<CancellationToken>()).Returns(recipient);
        _currentUserContext.UserId.Returns(userId);

        // Act
        var result = await _handler.Handle(new DeleteNotificationCommand(recipient.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Received(1).RemoveNotificationRecipient(recipient);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
