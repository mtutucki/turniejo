using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AddGoal;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class AddGoalCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly ISender _sender;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddGoalCommandHandler _handler;

        public AddGoalCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _sender = Substitute.For<ISender>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var rosterGuard = new MatchEventRosterGuard(_playerModule);
            _handler = new AddGoalCommandHandler(_dbContext, rosterGuard, _sender, _currentUserContext);
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
        public async Task Handle_ShouldSucceed_AndTriggerRecalculation_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var scorerId = Guid.NewGuid();
            var assistId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId, scorerId, assistId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(1, 0)));

            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, scorerId, assistId, MatchEventType.Goal, 23, 15);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(1);
            await _dbContext.Received(1).AddMatchEventAsync(
                Arg.Is<MatchEvent>(e => e.Type == MatchEventType.Goal && e.TournamentTeamId == match.HomeTournamentTeamId && e.Minute == 23),
                Arg.Any<CancellationToken>());
            await _sender.Received(1).Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>());
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
            var command = new AddGoalCommand(Guid.NewGuid(), matchId, Guid.NewGuid(), null, null, MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotLiveOrPaused()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, null, null, MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamNotPlayingInMatch()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddGoalCommand(tournamentId, match.Id, Guid.NewGuid(), null, null, MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentTeamId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenScorerIsNotOnTeamRoster()
        {
            // Zamyka lukę z audytu: gol nie mógł być dopisany dowolnemu PlayerId, w tym zawodnikowi
            // drużyny przeciwnej — teraz PlayerId musi realnie być w składzie TournamentTeamId.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            SetUpRoster(match.HomeTournamentTeamId); // pusty skład — nikt nie jest przypisany
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, Guid.NewGuid(), null, MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
            await _dbContext.DidNotReceive().AddMatchEventAsync(Arg.Any<MatchEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenScorerBelongsToOpposingTeam()
        {
            // Konkretny scenariusz z audytu: zawodnik z drużyny przeciwnej nie może dostać gola
            // przypisanego do TournamentTeamId, do której nie należy.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var opposingPlayerId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId); // scorer nie jest tu
            SetUpRoster(match.AwayTournamentTeamId, opposingPlayerId); // tylko w drużynie przeciwnej
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, opposingPlayerId, null, MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenAssistPlayerIsNotOnTeamRoster()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            var scorerId = Guid.NewGuid();
            SetUpRoster(match.HomeTournamentTeamId, scorerId); // asystent nie jest w składzie
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, scorerId, Guid.NewGuid(), MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.AssistPlayerId");
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenPlayerIdIsNull()
        {
            // Nieznany strzelec (PlayerId=null) to legalny przypadek — guard nie powinien go blokować.
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(1, 0)));
            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, null, null, MatchEventType.Goal, 10, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
    }
}
