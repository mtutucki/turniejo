using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildTeam;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.PlayerGuardians;

public sealed class GetMyChildTeamQueryHandlerTests
{
    private readonly IPlayersDbContext _dbContext;
    private readonly ITeamModule _teamModule;
    private readonly ITournamentModule _tournamentModule;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly GetMyChildTeamQueryHandler _handler;

    public GetMyChildTeamQueryHandlerTests()
    {
        _dbContext = Substitute.For<IPlayersDbContext>();
        _teamModule = Substitute.For<ITeamModule>();
        _tournamentModule = Substitute.For<ITournamentModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();

        var accessChecker = new GuardianAccessChecker(_dbContext);
        _handler = new GetMyChildTeamQueryHandler(_dbContext, accessChecker, _teamModule, _tournamentModule, _currentUserContext);
    }

    [Fact]
    public async Task Handle_ShouldThrowUnauthorized_WhenCallerIsNotAGuardian()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(Guid.NewGuid());
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian>());

        // Act
        var act = () => _handler.Handle(new GetMyChildTeamQuery(playerId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnTeamMemberships_ForGuardiansChild()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentTeamId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var tournamentId = Guid.NewGuid();

        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });

        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, 7, PlayerPosition.Midfielder, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });

        _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, teamId, Guid.NewGuid(), Guid.NewGuid(), "Accepted"));
        _teamModule.GetTeamAsync(teamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(teamId, "Lions", "LIO"));
        _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 8));

        // Act
        var result = await _handler.Handle(new GetMyChildTeamQuery(playerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].TeamName.Should().Be("Lions");
        result.Value[0].TournamentName.Should().Be("Cup");
        result.Value[0].ShirtNumber.Should().Be(7);
    }

    [Theory]
    [InlineData("Withdrawn")]
    [InlineData("Rejected")]
    public async Task Handle_ShouldSkipMembership_WhenTeamWasWithdrawnOrRejected(string registrationStatus)
    {
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tournamentTeamId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });
        var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, 7, PlayerPosition.Midfielder, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { tournamentPlayer });
        _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(tournamentTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), registrationStatus));

        var result = await _handler.Handle(new GetMyChildTeamQuery(playerId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSkipMembership_WhenTournamentTeamLookupMisses()
    {
        // Dziecko może mieć TournamentPlayer wskazujący na drużynę, która już nie istnieje/nie jest
        // dostępna (np. usunięta z turnieju) — handler ma go pominąć, nie zwrócić błędnego wpisu.
        var playerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, userId, RelationshipType.Parent, false, true, Guid.NewGuid()).Value;
        _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<PlayerGuardian> { guardian });

        var orphanedTournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), Guid.NewGuid(), playerId, null, null, Guid.NewGuid()).Value;
        _dbContext.GetTournamentPlayersByPlayerIdAsync(playerId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer> { orphanedTournamentPlayer });
        _teamModule.GetTournamentTeamAsync(orphanedTournamentPlayer.TournamentTeamId, Arg.Any<CancellationToken>())
            .Returns((TournamentTeamDto?)null);

        // Act
        var result = await _handler.Handle(new GetMyChildTeamQuery(playerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
