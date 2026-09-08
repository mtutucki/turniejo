using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Matches.UpdateMatchEvent;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class UpdateMatchEventCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ISender _sender;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly UpdateMatchEventCommandHandler _handler;

        public UpdateMatchEventCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _sender = Substitute.For<ISender>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new UpdateMatchEventCommandHandler(_dbContext, _sender, _currentUserContext);

            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(1, 0)));
        }

        private static Match CreateLiveMatch()
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldUpdateEventAndRecalculateScore_WhenDataIsValid()
        {
            // Arrange
            var match = CreateLiveMatch();
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            var newPlayerId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new UpdateMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id, MatchEventType.OwnGoal, 25, null, newPlayerId, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            matchEvent.Type.Should().Be(MatchEventType.OwnGoal);
            matchEvent.Minute.Should().Be(25);
            matchEvent.PlayerId.Should().Be(newPlayerId);
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.EntityType == "MatchEvent" && a.Action == "Update"),
                Arg.Any<CancellationToken>());
            await _sender.Received(1).Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new UpdateMatchEventCommand(Guid.NewGuid(), matchId, Guid.NewGuid(), MatchEventType.Goal, 10, null, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenEventNotFound()
        {
            // Arrange
            var match = CreateLiveMatch();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((MatchEvent?)null);
            var command = new UpdateMatchEventCommand(match.TournamentId, match.Id, Guid.NewGuid(), MatchEventType.Goal, 10, null, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("MatchEvent.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotEditable()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            var command = new UpdateMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id, MatchEventType.Goal, 10, null, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNewMinuteIsNegative()
        {
            // Gałąź walidacji domenowej (MatchEvent.UpdateDetails) — nietestowana wcześniej, handler
            // musi poprawnie propagować Result.Failure z encji, nie tylko z własnych sprawdzeń NotFound/Status.
            var match = CreateLiveMatch();
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            var command = new UpdateMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id, MatchEventType.Goal, -1, null, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Minute");
            await _dbContext.DidNotReceive().AddAuditLogAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenRecalculationFails()
        {
            // Event i tak zostaje zaktualizowany i zapisany zanim recalc się odpali — handler musi
            // mimo to zwrócić Failure, gdy przeliczenie wyniku nie powiedzie się.
            var match = CreateLiveMatch();
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Failure<RecalculateMatchScoreResponse>(Error.NotFound("Match", match.Id)));
            var command = new UpdateMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id, MatchEventType.Goal, 10, null, null, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
