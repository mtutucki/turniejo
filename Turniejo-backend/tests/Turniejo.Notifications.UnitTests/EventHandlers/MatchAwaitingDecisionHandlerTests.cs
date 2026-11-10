using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Application.Services;
using Turniejo.Notifications.Domain.Entities;
using Turniejo.Notifications.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.EventHandlers;

public sealed class MatchAwaitingDecisionHandlerTests
{
    private readonly INotificationsDbContext _dbContext = Substitute.For<INotificationsDbContext>();
    private readonly IUserModule _userModule = Substitute.For<IUserModule>();
    private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
    private readonly MatchAwaitingDecisionHandler _handler;
    private readonly Guid _tournamentId = Guid.NewGuid();
    private readonly Guid _home = Guid.NewGuid();
    private readonly Guid _away = Guid.NewGuid();

    public MatchAwaitingDecisionHandlerTests()
    {
        _handler = new MatchAwaitingDecisionHandler(new NotificationPublisher(_dbContext), _userModule, _teamModule, _dbContext);
        GivenTeam(_home, "Orły");
        GivenTeam(_away, "Sokoły");
    }

    private void GivenTeam(Guid tournamentTeamId, string name)
    {
        var teamId = Guid.NewGuid();
        _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(tournamentTeamId, _tournamentId, teamId, Guid.NewGuid(), null, "Accepted"));
        _teamModule.GetTeamAsync(teamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(teamId, name, null));
    }

    private Guid GivenUser(string role, string scopeType = "Tournament")
    {
        var userId = Guid.NewGuid();
        _userModule.GetUserRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns([new UserRoleDto(userId, role, scopeType, _tournamentId)]);
        return userId;
    }

    private MatchAwaitingDecision Event() => new(Guid.NewGuid(), _tournamentId, _home, _away, 1, 1);

    [Fact]
    public async Task Handle_ShouldNotifyOnlyTheTournamentsOrganizers_NamingTheMatch()
    {
        var organizer = GivenUser("Organizer");
        var referee = GivenUser("Referee");
        _userModule.GetUserIdsByScopeAsync("Tournament", _tournamentId, Arg.Any<CancellationToken>()).Returns([organizer, referee]);
        var e = Event();

        await _handler.Handle(e, CancellationToken.None);

        await _dbContext.Received(1).AddNotificationAsync(
            Arg.Is<Notification>(n =>
                n.Type == NotificationType.MatchAwaitingDecision && n.MatchId == e.MatchId &&
                n.Message.Contains("Orły - Sokoły (1:1)")),
            Arg.Any<CancellationToken>());
        await _dbContext.Received(1).AddNotificationRecipientsAsync(
            Arg.Is<IReadOnlyCollection<NotificationRecipient>>(r => r.Count == 1 && r.Single().UserId == organizer),
            Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenTheTournamentHasNoOrganizer()
    {
        var referee = GivenUser("Referee");
        _userModule.GetUserIdsByScopeAsync("Tournament", _tournamentId, Arg.Any<CancellationToken>()).Returns([referee]);

        await _handler.Handle(Event(), CancellationToken.None);

        await _dbContext.DidNotReceive().AddNotificationAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
