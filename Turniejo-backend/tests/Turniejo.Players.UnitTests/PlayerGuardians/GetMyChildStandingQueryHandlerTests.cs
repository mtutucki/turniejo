using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildStanding;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.PlayerGuardians;

public sealed class GetMyChildStandingQueryHandlerTests
{
    private readonly IPlayersDbContext _dbContext;
    private readonly ITeamModule _teamModule;
    private readonly IStandingModule _standingModule;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly GetMyChildStandingQueryHandler _handler;

    public GetMyChildStandingQueryHandlerTests()
    {
        _dbContext = Substitute.For<IPlayersDbContext>();
        _teamModule = Substitute.For<ITeamModule>();
        _standingModule = Substitute.For<IStandingModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();

        var accessChecker = new GuardianAccessChecker(_dbContext);
        _handler = new GetMyChildStandingQueryHandler(_dbContext, accessChecker, _teamModule, _standingModule, _currentUserContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnGroupStandings_WhenChildTeamHasGroup()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentTeamId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);

        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });

        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), groupId, "Accepted"));

        var standings = new List<GroupStandingDto> { new(tournamentTeamId, 1, 9) };
        _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>()).Returns(standings);

        // Act
        var result = await _handler.Handle(new GetMyChildStandingQuery(playerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].GroupId.Should().Be(groupId);
        result.Value[0].Standings.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldSkipTeam_WhenNotAssignedToGroup()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentTeamId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);

        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });

        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));

        // Act
        var result = await _handler.Handle(new GetMyChildStandingQuery(playerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
