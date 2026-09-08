using FluentAssertions;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;

namespace Turniejo.Notifications.UnitTests.Entities;

public sealed class NotificationRecipientTests
{
    [Fact]
    public void Create_ShouldSucceed_WithValidData()
    {
        // Act
        var result = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(NotificationRecipientStatus.Pending);
    }

    [Fact]
    public void MarkAsRead_ShouldTransitionToRead_AndSetReadAt_AndRaiseEvent()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;

        // Act
        var result = recipient.MarkAsRead();

        // Assert
        result.IsSuccess.Should().BeTrue();
        recipient.Status.Should().Be(NotificationRecipientStatus.Read);
        recipient.ReadAt.Should().NotBeNull();
        recipient.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void MarkAsRead_ShouldBeIdempotent_WhenAlreadyRead()
    {
        // Arrange
        var recipient = NotificationRecipient.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
        recipient.MarkAsRead();
        recipient.ClearDomainEvents();

        // Act
        var result = recipient.MarkAsRead();

        // Assert
        result.IsSuccess.Should().BeTrue();
        recipient.DomainEvents.Should().BeEmpty();
    }
}
