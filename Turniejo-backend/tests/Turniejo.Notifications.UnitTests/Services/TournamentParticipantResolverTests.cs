using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.Services;

public sealed class TournamentParticipantResolverTests
{
    private readonly IUserModule _userModule = Substitute.For<IUserModule>();
    private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
    private readonly IMatchModule _matchModule = Substitute.For<IMatchModule>();
    private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
    private readonly TournamentParticipantResolver _resolver;

    public TournamentParticipantResolverTests()
    {
        _resolver = new TournamentParticipantResolver(_userModule, _teamModule, _matchModule, _playerModule);
    }

    [Fact]
    public async Task ResolveAsync_ShouldIncludeGuardiansWithCanReceiveNotifications_ForPlayersOnTournamentTeams()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();

        _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([new TournamentTeamDto(teamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted")]);
        _teamModule.GetTeamStaffUserIdsAsync(teamId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _playerModule.GetTournamentTeamPlayersAsync(teamId, Arg.Any<CancellationToken>())
            .Returns([new TournamentTeamPlayerDto(Guid.NewGuid(), teamId, playerId, "Jan", "Kowalski", 9, "Forward", "Active")]);
        _playerModule.GetNotifiableGuardianUserIdsForPlayersAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(playerId)), Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { guardianId });
        _matchModule.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([]);

        var recipients = await _resolver.ResolveAsync(tournamentId, CancellationToken.None);

        recipients.Should().ContainSingle().Which.Should().Be(guardianId);
    }

    [Fact]
    public async Task ResolveAsync_ShouldNotCallGuardianLookup_WhenNoPlayersOnAnyTeam()
    {
        var tournamentId = Guid.NewGuid();

        _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([]);
        _matchModule.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([]);

        var recipients = await _resolver.ResolveAsync(tournamentId, CancellationToken.None);

        recipients.Should().BeEmpty();
        await _playerModule.DidNotReceive().GetNotifiableGuardianUserIdsForPlayersAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_ShouldDeduplicate_WhenSamePersonHoldsMultipleRoles()
    {
        var tournamentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var matchId = Guid.NewGuid();
        var organizerAndRefereeId = Guid.NewGuid();

        _userModule.GetUserIdsByScopeAsync("Tournament", tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { organizerAndRefereeId });
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([new TournamentTeamDto(teamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted")]);
        _teamModule.GetTeamStaffUserIdsAsync(teamId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid>());
        _playerModule.GetTournamentTeamPlayersAsync(teamId, Arg.Any<CancellationToken>())
            .Returns([]);
        _matchModule.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns([new TournamentMatchDto(matchId, tournamentId, "Scheduled", DateTimeOffset.UtcNow, teamId, Guid.NewGuid(), 0, 0)]);
        _userModule.GetUserIdsByScopeAsync("Match", matchId, Arg.Any<CancellationToken>())
            .Returns(new List<Guid> { organizerAndRefereeId });

        var recipients = await _resolver.ResolveAsync(tournamentId, CancellationToken.None);

        recipients.Should().ContainSingle().Which.Should().Be(organizerAndRefereeId);
    }
}
