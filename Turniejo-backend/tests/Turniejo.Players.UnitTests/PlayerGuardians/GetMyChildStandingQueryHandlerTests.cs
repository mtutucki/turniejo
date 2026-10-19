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
    private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
    private readonly ICurrentUserContext _currentUserContext;
    private readonly GetMyChildStandingQueryHandler _handler;

    public GetMyChildStandingQueryHandlerTests()
    {
        _dbContext = Substitute.For<IPlayersDbContext>();
        _teamModule = Substitute.For<ITeamModule>();
        _standingModule = Substitute.For<IStandingModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();

        var accessChecker = new GuardianAccessChecker(_dbContext);
        _handler = new GetMyChildStandingQueryHandler(_dbContext, accessChecker, _teamModule, _standingModule, _tournamentModule, _currentUserContext);
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
    [Fact]
    public async Task Handle_ShouldIncludeGroupAndTeamNames()
    {
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var childTeamId = Guid.NewGuid();
        var rivalTeamId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), childTeamId, playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        var childTeam = Guid.NewGuid();
        var rivalTeam = Guid.NewGuid();
        _teamModule.GetTournamentTeamAsync(childTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(childTeamId, tournamentId, childTeam, Guid.NewGuid(), groupId, "Accepted"));
        _teamModule.GetTournamentTeamAsync(rivalTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(rivalTeamId, tournamentId, rivalTeam, Guid.NewGuid(), groupId, "Accepted"));
        _teamModule.GetTeamAsync(childTeam, Arg.Any<CancellationToken>()).Returns(new TeamDto(childTeam, "Orły", null));
        _teamModule.GetTeamAsync(rivalTeam, Arg.Any<CancellationToken>()).Returns(new TeamDto(rivalTeam, "Lwy", null));
        _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new TournamentGroupDto(groupId, tournamentId, Guid.NewGuid(), "Grupa A"));
        _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new List<GroupStandingDto> { new(rivalTeamId, 1, 3, 1, 1, 0, 0, 2, 0), new(childTeamId, 2, 0, 1, 0, 0, 1, 0, 2) });

        var result = await _handler.Handle(new GetMyChildStandingQuery(playerId), CancellationToken.None);

        var standing = result.Value.Single();
        standing.GroupName.Should().Be("Grupa A");
        standing.TeamNames![childTeamId].Should().Be("Orły");
        standing.TeamNames[rivalTeamId].Should().Be("Lwy");
        standing.Standings[0].GoalsFor.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldListWholeGroupWithZeros_BeforeAnyMatchIsFinished()
    {
        var userId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var childTeamId = Guid.NewGuid();
        var rivalTeamId = Guid.NewGuid();
        var otherGroupTeamId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), childTeamId, playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        var childTeam = new TournamentTeamDto(childTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), groupId, "Accepted");
        var rivalTeam = new TournamentTeamDto(rivalTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), groupId, "Accepted");
        var otherTeam = new TournamentTeamDto(otherGroupTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Accepted");
        _teamModule.GetTournamentTeamAsync(childTeamId, Arg.Any<CancellationToken>()).Returns(childTeam);
        _teamModule.GetTournamentTeamAsync(rivalTeamId, Arg.Any<CancellationToken>()).Returns(rivalTeam);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<TournamentTeamDto> { childTeam, rivalTeam, otherTeam });
        _teamModule.GetTeamAsync(rivalTeam.TeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(rivalTeam.TeamId, "Lwy", null));
        _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>()).Returns(new List<GroupStandingDto>());

        var result = await _handler.Handle(new GetMyChildStandingQuery(playerId), CancellationToken.None);

        var standing = result.Value.Single();
        standing.Standings.Select(r => r.TournamentTeamId).Should().BeEquivalentTo(new[] { childTeamId, rivalTeamId });
        standing.Standings.Should().OnlyContain(r => r.Points == 0 && r.MatchesPlayed == 0);
        standing.Standings.Select(r => r.Position).Should().BeEquivalentTo(new[] { 1, 2 });
        standing.TeamNames![rivalTeamId].Should().Be("Lwy");
    }

    [Fact]
    public async Task Handle_ShouldNotListWithdrawnTeams_ThatStillHaveGroupId()
    {
        var userId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var childTeamId = Guid.NewGuid();
        var withdrawnTeamId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), childTeamId, playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        var childTeam = new TournamentTeamDto(childTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), groupId, "Accepted");
        var withdrawnTeam = new TournamentTeamDto(withdrawnTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), groupId, "Withdrawn");
        _teamModule.GetTournamentTeamAsync(childTeamId, Arg.Any<CancellationToken>()).Returns(childTeam);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(new List<TournamentTeamDto> { childTeam, withdrawnTeam });
        _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>()).Returns(new List<GroupStandingDto>());

        var result = await _handler.Handle(new GetMyChildStandingQuery(playerId), CancellationToken.None);

        result.Value.Single().Standings.Select(r => r.TournamentTeamId).Should().Equal(childTeamId);
    }

    [Fact]
    public async Task Handle_ShouldSkipTeam_WhenTournamentIsKnockoutOnly()
    {
        var userId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var childTeamId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), childTeamId, playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        _teamModule.GetTournamentTeamAsync(childTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(childTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), groupId, "Accepted"));
        _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(new TournamentDto(tournamentId, "Puchar", "RegistrationClosed", "Knockout", Guid.NewGuid(), 4));
        _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
            .Returns(new List<GroupStandingDto> { new(childTeamId, 1, 0) });

        var result = await _handler.Handle(new GetMyChildStandingQuery(playerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
