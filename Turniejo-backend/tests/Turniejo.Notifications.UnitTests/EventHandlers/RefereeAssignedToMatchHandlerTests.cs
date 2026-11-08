using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Enums;
using NotificationEntity = Turniejo.Notifications.Domain.Entities.Notification;
using RecipientEntity = Turniejo.Notifications.Domain.Entities.NotificationRecipient;

namespace Turniejo.Notifications.UnitTests.EventHandlers;

// QA v3: MNZ-031 - sędzia zdjęty z meczu przez organizatora nie dostawał żadnej informacji.
public sealed class RefereeAssignedToMatchHandlerTests
{
    private readonly INotificationsDbContext _dbContext = Substitute.For<INotificationsDbContext>();
    private readonly List<NotificationEntity> _published = [];
    private readonly List<Guid> _recipients = [];
    private readonly RefereeAssignedToMatchHandler _handler;

    public RefereeAssignedToMatchHandlerTests()
    {
        _dbContext.AddNotificationAsync(Arg.Do<NotificationEntity>(n => _published.Add(n)), Arg.Any<CancellationToken>());
        _dbContext.AddNotificationRecipientsAsync(Arg.Do<IReadOnlyCollection<RecipientEntity>>(r => _recipients.AddRange(r.Select(x => x.UserId))), Arg.Any<CancellationToken>());
        _handler = new RefereeAssignedToMatchHandler(new NotificationPublisher(_dbContext), _dbContext);
    }

    [Fact]
    public async Task MNZ_031_PreviousReferee_IsNotified_WhenOrganizerAssignsSomeoneElse()
    {
        var previous = Guid.NewGuid();
        var organizer = Guid.NewGuid();

        await _handler.Handle(new RefereeAssignedToMatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), organizer, previous), CancellationToken.None);

        _published.Should().ContainSingle().Which.Type.Should().Be(NotificationType.RefereeChanged);
        _recipients.Should().Equal(previous);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NoNotification_WhenRefereeReleasesTheMatchThemselves()
    {
        var referee = Guid.NewGuid();

        await _handler.Handle(new RefereeAssignedToMatch(Guid.NewGuid(), Guid.NewGuid(), null, referee, referee), CancellationToken.None);

        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task NoNotification_WhenMatchHadNoReferee()
    {
        await _handler.Handle(new RefereeAssignedToMatch(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        _published.Should().BeEmpty();
    }
}
