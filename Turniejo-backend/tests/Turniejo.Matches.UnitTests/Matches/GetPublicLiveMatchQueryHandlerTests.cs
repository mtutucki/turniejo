using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetPublicLiveMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetPublicLiveMatchQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IPlayerModule _playerModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicLiveMatchQueryHandler _handler;

        public GetPublicLiveMatchQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _playerModule = Substitute.For<IPlayerModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicLiveMatchQueryHandler(_dbContext, _teamModule, _playerModule, _tournamentModule);

            _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((TournamentTeamDto?)null);
            _playerModule.GetTournamentTeamPlayersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeamPlayerDto>());
            _dbContext.GetMatchEventsByMatchIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<MatchEvent>());
        }

        private static Match CreateLiveMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, StageType.Group).Value;
            match.Start(Guid.NewGuid());
            return match;
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetPublicLiveMatchQuery(matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchIsNotLive()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            // Act
            var result = await _handler.Handle(new GetPublicLiveMatchQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShowLiveMatchesIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, false));

            // Act
            var result = await _handler.Handle(new GetPublicLiveMatchQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnLivePanel_WhenMatchIsLiveAndTournamentIsPublic()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, true));

            // Act
            var result = await _handler.Handle(new GetPublicLiveMatchQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Status.Should().Be("Live");
        }
    }
}
