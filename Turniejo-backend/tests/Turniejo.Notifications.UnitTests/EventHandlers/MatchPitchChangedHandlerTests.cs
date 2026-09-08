using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.EventHandlers;

public sealed class MatchPitchChangedHandlerTests
{
    private readonly INotificationsDbContext _dbContext;
    private readonly IMatchModule _matchModule;
    private readonly ITeamModule _teamModule;
    private readonly MatchPitchChangedHandler _handler;

    public MatchPitchChangedHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _matchModule = Substitute.For<IMatchModule>();
        _teamModule = Substitute.For<ITeamModule>();

        var publisher = new NotificationPublisher(_dbContext);
        var resolver = new MatchNotificationRecipientResolver(_teamModule);

        _handler = new MatchPitchChangedHandler(publisher, resolver, _matchModule, _dbContext);
    }

    [Fact]
    public async Task Handle_ShouldPublishNotification_AndSave_WhenRecipientsExist()
    {
        // Arrange
        var matchId = Guid.NewGuid();
        var refereeId = Guid.NewGuid();
        var match = new MatchInfoDto(matchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), refereeId, Guid.NewGuid(), DateTimeOffset.UtcNow, "Scheduled");
        _matchModule.GetMatchAsync(matchId, Arg.Any<CancellationToken>()).Returns(match);
        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var notification = new MatchPitchChanged(matchId, match.TournamentId, Guid.NewGuid(), Guid.NewGuid());

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
        var notification = new MatchPitchChanged(matchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

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
        var match = new MatchInfoDto(matchId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), DateTimeOffset.UtcNow, "Scheduled");
        _matchModule.GetMatchAsync(matchId, Arg.Any<CancellationToken>()).Returns(match);
        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        var notification = new MatchPitchChanged(matchId, match.TournamentId, Guid.NewGuid(), Guid.NewGuid());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        await _dbContext.DidNotReceive().AddNotificationAsync(Arg.Any<Turniejo.Notifications.Domain.Entities.Notification>(), Arg.Any<CancellationToken>());
    }
}
