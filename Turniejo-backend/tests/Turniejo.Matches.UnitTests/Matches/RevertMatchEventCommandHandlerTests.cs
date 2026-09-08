using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Matches.RevertMatchEvent;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class RevertMatchEventCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ISender _sender;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly RevertMatchEventCommandHandler _handler;

        public RevertMatchEventCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _sender = Substitute.For<ISender>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new RevertMatchEventCommandHandler(_dbContext, _sender, _currentUserContext);

            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(0, 0)));
        }

        private static Match CreateLiveMatch()
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldMarkEventDeleted_AndRecalculateScore_WhenDataIsValid()
        {
            // Arrange
            var match = CreateLiveMatch();
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new RevertMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            matchEvent.IsDeleted.Should().BeTrue();
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.EntityType == "MatchEvent" && a.Action == "Revert" && a.EntityId == matchEvent.Id),
                Arg.Any<CancellationToken>());
            await _sender.Received(1).Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new RevertMatchEventCommand(Guid.NewGuid(), matchId, Guid.NewGuid());

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
            var command = new RevertMatchEventCommand(match.TournamentId, match.Id, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("MatchEvent.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenAlreadyReverted()
        {
            // Arrange
            var match = CreateLiveMatch();
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            matchEvent.MarkDeleted(Guid.NewGuid(), match.TournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            var command = new RevertMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.IsDeleted");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotEditable()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            var matchEvent = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(matchEvent.Id, Arg.Any<CancellationToken>()).Returns(matchEvent);
            var command = new RevertMatchEventCommand(match.TournamentId, match.Id, matchEvent.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }
    }
}
