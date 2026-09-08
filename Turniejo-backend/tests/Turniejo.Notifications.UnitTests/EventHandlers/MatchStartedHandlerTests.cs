using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.EventHandlers;

public sealed class MatchStartedHandlerTests
{
    private readonly INotificationsDbContext _dbContext;
    private readonly IMatchModule _matchModule;
    private readonly ITeamModule _teamModule;
    private readonly MatchStartedHandler _handler;

    public MatchStartedHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _matchModule = Substitute.For<IMatchModule>();
        _teamModule = Substitute.For<ITeamModule>();

        var publisher = new NotificationPublisher(_dbContext);
        var resolver = new MatchNotificationRecipientResolver(_teamModule);

        _handler = new MatchStartedHandler(publisher, resolver, _matchModule, _dbContext);
    }

    [Fact]
    public async Task Handle_ShouldPublishNotification_AndSave_WhenRecipientsExist()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var refereeId = Guid.NewGuid();
        var match = new MatchInfoDto(matchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), refereeId, null, DateTimeOffset.UtcNow, "Live");
        _matchModule.GetMatchAsync(matchId, Arg.Any<CancellationToken>()).Returns(match);
        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var notification = new MatchStarted(matchId, match.TournamentId, DateTimeOffset.UtcNow, Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddNotificationAsync(Arg.Any<Turniejo.Notifications.Domain.Entities.Notification>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenMatchNotFound()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        _matchModule.GetMatchAsync(matchId, Arg.Any<CancellationToken>()).Returns((MatchInfoDto?)null);
        var notification = new MatchStarted(matchId, Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoRecipientsResolved()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var match = new MatchInfoDto(matchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, DateTimeOffset.UtcNow, "Live");
        _matchModule.GetMatchAsync(matchId, Arg.Any<CancellationToken>()).Returns(match);
        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var notification = new MatchStarted(matchId, match.TournamentId, DateTimeOffset.UtcNow, Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _dbContext.DidNotReceive().AddNotificationAsync(Arg.Any<Turniejo.Notifications.Domain.Entities.Notification>(), Arg.Any<CancellationToken>());
    }
}
