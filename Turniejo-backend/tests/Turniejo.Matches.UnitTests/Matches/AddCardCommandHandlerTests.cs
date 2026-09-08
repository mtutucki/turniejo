using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AddCard;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class AddCardCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddCardCommandHandler _handler;

        public AddCardCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var rosterGuard = new MatchEventRosterGuard(_playerModule);
            _handler = new AddCardCommandHandler(_dbContext, rosterGuard, _currentUserContext);
        }

        private static Match CreateLiveMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        private void SetUpRoster(Guid tournamentTeamId, params Guid[] playerIds)
        {
            _playerModule.GetTournamentTeamPlayersAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(playerIds
                    .Select(id => new TournamentTeamPlayerDto(Guid.NewGuid(), tournamentTeamId, id, "Jan", "Kowalski", null, null, "Active"))
                    .ToList());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var playerId = Guid.NewGuid();
            SetUpRoster(match.AwayTournamentTeamId, playerId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddCardCommand(tournamentId, match.Id, match.AwayTournamentTeamId, playerId, MatchEventType.YellowCard, 55, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddMatchEventAsync(
                Arg.Is<MatchEvent>(e => e.Type == MatchEventType.YellowCard && e.PlayerId == playerId && e.Minute == 55),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchEventAdded")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new AddCardCommand(Guid.NewGuid(), matchId, Guid.NewGuid(), Guid.NewGuid(), MatchEventType.YellowCard, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamNotPlayingInMatch()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddCardCommand(tournamentId, match.Id, Guid.NewGuid(), Guid.NewGuid(), MatchEventType.RedCard, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentTeamId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerIsNotOnTeamRoster()
        {
            // Zamyka lukę z audytu: kartkę dało się przypisać dowolnemu PlayerId.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            SetUpRoster(match.AwayTournamentTeamId); // pusty skład
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddCardCommand(tournamentId, match.Id, match.AwayTournamentTeamId, Guid.NewGuid(), MatchEventType.YellowCard, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
            await _dbContext.DidNotReceive().AddMatchEventAsync(Arg.Any<MatchEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerBelongsToOpposingTeam()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var opposingPlayerId = Guid.NewGuid();
            SetUpRoster(match.AwayTournamentTeamId);
            SetUpRoster(match.HomeTournamentTeamId, opposingPlayerId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddCardCommand(tournamentId, match.Id, match.AwayTournamentTeamId, opposingPlayerId, MatchEventType.YellowCard, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
        }
    }
}
