using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AddGoal;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class AddGoalCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ISender _sender;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddGoalCommandHandler _handler;

        public AddGoalCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _sender = Substitute.For<ISender>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new AddGoalCommandHandler(_dbContext, _sender, _currentUserContext);
        }

        private static Match CreateLiveMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_AndTriggerRecalculation_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateLiveMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(1, 0)));

            var command = new AddGoalCommand(tournamentId, match.Id, match.HomeTournamentTeamId, Guid.NewGuid(), null, MatchEventType.Goal, 23, 15);

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
    }
}
