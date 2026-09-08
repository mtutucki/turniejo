using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AddAssist;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class AddAssistCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddAssistCommandHandler _handler;

        public AddAssistCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var rosterGuard = new MatchEventRosterGuard(_playerModule);
            _handler = new AddAssistCommandHandler(_dbContext, rosterGuard, _currentUserContext);
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
        public async Task Handle_ShouldAttachAssistToExistingUnassistedGoal_WhenDataIsValid()
        {
            // Domyka lukę z audytu: asysta miała dwie niezależne ścieżki zapisu (AssistPlayerId przy
            // AddGoal i osobny event Assist tutaj), co groziło podwójnym naliczeniem w rankingu.
            // Teraz jest jedna ścieżka — RelatedPlayerId na już istniejącym evencie gola.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var assistingPlayerId = Guid.NewGuid();
            var scorerPlayerId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId, assistingPlayerId, scorerPlayerId);
            var goalEvent = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.Goal, 30, Guid.NewGuid(),
                match.HomeTournamentTeamId, scorerPlayerId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([goalEvent]);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddAssistCommand(tournamentId, match.Id, match.HomeTournamentTeamId, assistingPlayerId, scorerPlayerId, 30, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchEventId.Should().Be(goalEvent.Id);
            goalEvent.RelatedPlayerId.Should().Be(assistingPlayerId);
            await _dbContext.DidNotReceive().AddMatchEventAsync(Arg.Any<MatchEvent>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchEventUpdated")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNoMatchingUnassistedGoalExists()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var assistingPlayerId = Guid.NewGuid();
            var scorerPlayerId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId, assistingPlayerId, scorerPlayerId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([]);
            var command = new AddAssistCommand(tournamentId, match.Id, match.HomeTournamentTeamId, assistingPlayerId, scorerPlayerId, 30, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ScorerPlayerId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGoalAlreadyHasAnAssist()
        {
            // Bez tego dało się dopisać drugą asystę do tego samego gola.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var assistingPlayerId = Guid.NewGuid();
            var scorerPlayerId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId, assistingPlayerId, scorerPlayerId);
            var goalEvent = MatchEvent.Create(
                Guid.NewGuid(), match.Id, MatchEventType.Goal, 30, Guid.NewGuid(),
                match.HomeTournamentTeamId, scorerPlayerId, Guid.NewGuid()).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([goalEvent]);
            var command = new AddAssistCommand(tournamentId, match.Id, match.HomeTournamentTeamId, assistingPlayerId, scorerPlayerId, 30, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ScorerPlayerId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new AddAssistCommand(Guid.NewGuid(), matchId, Guid.NewGuid(), Guid.NewGuid(), null, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenAssistingPlayerIsNotOnTeamRoster()
        {
            // Zamyka lukę z audytu: asystę dało się przypisać dowolnemu PlayerId.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            SetUpRoster(match.HomeTournamentTeamId); // pusty skład
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddAssistCommand(tournamentId, match.Id, match.HomeTournamentTeamId, Guid.NewGuid(), null, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
            await _dbContext.DidNotReceive().AddMatchEventAsync(Arg.Any<MatchEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenScorerIsNotOnTeamRoster()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var assistingPlayerId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId, assistingPlayerId); // scorer nie jest w składzie
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddAssistCommand(tournamentId, match.Id, match.HomeTournamentTeamId, assistingPlayerId, Guid.NewGuid(), 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ScorerPlayerId");
        }
    }
}
