using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildResults;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.PlayerGuardians;

public sealed class GetMyChildResultsQueryHandlerTests
{
    private readonly IPlayersDbContext _dbContext;
    private readonly IMatchModule _matchModule;
    private readonly ITeamModule _teamModule;
    private readonly ITournamentModule _tournamentModule;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly GetMyChildResultsQueryHandler _handler;

    public GetMyChildResultsQueryHandlerTests()
    {
        _dbContext = Substitute.For<IPlayersDbContext>();
        _matchModule = Substitute.For<IMatchModule>();
        _teamModule = Substitute.For<ITeamModule>();
        _tournamentModule = Substitute.For<ITournamentModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();

        var accessChecker = new GuardianAccessChecker(_dbContext);
        var resolver = new MyChildMatchResolver(_dbContext, _matchModule, _teamModule, _tournamentModule);
        _handler = new GetMyChildResultsQueryHandler(resolver, accessChecker, _currentUserContext);

        _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
        _tournamentModule.GetTournamentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentDto?)null);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyFinishedMatches_WithScores()
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

        var matches = new List<TournamentMatchDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Scheduled", DateTimeOffset.UtcNow.AddDays(1), tournamentTeamId, Guid.NewGuid(), 0, 0),
            new(Guid.NewGuid(), Guid.NewGuid(), "Finished", DateTimeOffset.UtcNow.AddDays(-1), tournamentTeamId, Guid.NewGuid(), 3, 2),
        };
        _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(matches);

        // Act
        var result = await _handler.Handle(new GetMyChildResultsQuery(playerId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].HomeScore.Should().Be(3);
        result.Value[0].AwayScore.Should().Be(2);
    }
}
