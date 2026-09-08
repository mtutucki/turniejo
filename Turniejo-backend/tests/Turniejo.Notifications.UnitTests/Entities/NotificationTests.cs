using FluentAssertions;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;

namespace Turniejo.Notifications.UnitTests.Entities;

public sealed class NotificationTests
{
    [Fact]
    public void Create_ShouldSucceed_WithValidData()
    {
        // Act
        var result = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.MatchStarted, "Tytuł", "Wiadomość", Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(NotificationType.MatchStarted);
        result.Value.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Create_ShouldAllowNullTournamentId_ForSystemNotifications()
    {
        // Act
        var result = Notification.Create(
            Guid.NewGuid(), null, NotificationType.OrganizerAnnouncement, "Tytuł", "Wiadomość", null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TournamentId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_WhenTitleIsEmpty()
    {
        // Act
        var result = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.MatchStarted, "", "Wiadomość", Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Title");
    }

    [Fact]
    public void Create_ShouldFail_WhenMessageIsEmpty()
    {
        // Act
        var result = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.MatchStarted, "Tytuł", "", Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Message");
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var notification = Notification.Create(
            Guid.NewGuid(), Guid.NewGuid(), NotificationType.MatchStarted, "Tytuł", "Wiadomość", Guid.NewGuid()).Value;

        // Act
        notification.ClearDomainEvents();

        // Assert
        notification.DomainEvents.Should().BeEmpty();
    }
}
