using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.SetMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class SetMatchScoreCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly SetMatchScoreCommandHandler _handler;

        public SetMatchScoreCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new SetMatchScoreCommandHandler(_dbContext, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldSetScoreAndCreateAuditLog_WhenNotFinished()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new SetMatchScoreCommand(match.TournamentId, match.Id, 5, 3);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(5);
            result.Value.AwayScore.Should().Be(3);
            match.ResultType.Should().Be(MatchResultType.ManualDecision);
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.EntityType == "Match" && a.Action == "SetManualScore"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new SetMatchScoreCommand(Guid.NewGuid(), matchId, 1, 0);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchIsFinished()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.Finish(MatchResultType.Regular, null, 60, Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new SetMatchScoreCommand(match.TournamentId, match.Id, 1, 0);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }
    }
}
