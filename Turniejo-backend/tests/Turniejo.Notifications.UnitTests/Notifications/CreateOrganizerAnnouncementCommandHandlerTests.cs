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
    private readonly ITeamModule _teamModule;
    private readonly IMatchModule _matchModule;
    private readonly IPlayerModule _playerModule;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly CreateOrganizerAnnouncementCommandHandler _handler;

    public CreateOrganizerAnnouncementCommandHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _userModule = Substitute.For<IUserModule>();
        _teamModule = Substitute.For<ITeamModule>();
        _matchModule = Substitute.For<IMatchModule>();
        _playerModule = Substitute.For<IPlayerModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _currentUserContext.UserId.Returns(Guid.NewGuid());

        // Domyślnie brak drużyn/meczów, żeby TournamentParticipantResolver zwracał dokładnie to,
        // co GetUserIdsByScopeAsync("Tournament", ...) — reszta jego logiki (staff/sędziowie/rodzice)
        // jest przetestowana osobno w TournamentParticipantResolverTests.
        _teamModule.GetTournamentTeamsByTournamentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);
        _matchModule.GetMatchesByTournamentIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var publisher = new NotificationPublisher(_dbContext);
        var participantResolver = new TournamentParticipantResolver(_userModule, _teamModule, _matchModule, _playerModule);
        _handler = new CreateOrganizerAnnouncementCommandHandler(_dbContext, publisher, participantResolver, _currentUserContext);
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

    [Fact]
    public async Task Handle_ShouldAlsoReachTeamStaffAndReferees_NotJustTournamentScopedUsers()
    {
        // Wcześniej odbiorcy byli liczeni WYŁĄCZNIE przez GetUserIdsByScopeAsync("Tournament", ...) —
        // trenerzy (rola TournamentTeam-scoped) i sędziowie (rola Match-scoped) byli cicho pomijani.
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var organizerId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var refereeId = Guid.NewGuid();

        _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { organizerId });
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([new TournamentTeamDto(teamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted")]);
        _teamModule.GetTeamStaffUserIdsAsync(teamId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { coachId });
        _playerModule.GetTournamentTeamPlayersAsync(teamId, Arg.Any<CancellationToken>())
            .Returns([]);
        _matchModule.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([new TournamentMatchDto(matchId, tournamentId, "Scheduled", DateTimeOffset.UtcNow, teamId, Guid.NewGuid(), 0, 0)]);
        _userModule.GetUserIdsByScopeAsync("Match", matchId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { refereeId });

        var command = new CreateOrganizerAnnouncementCommand(tournamentId, "Tytuł", "Treść");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecipientCount.Should().Be(3);
    }
}
