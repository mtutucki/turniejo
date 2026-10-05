using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.CorrectFinishedMatchScore;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class CorrectFinishedMatchScoreCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly CorrectFinishedMatchScoreCommandHandler _handler;

        public CorrectFinishedMatchScoreCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new CorrectFinishedMatchScoreCommandHandler(_dbContext, _currentUserContext);
        }

        private static Match CreateFinishedMatch(int editWindowMinutes = 60)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.Finish(MatchResultType.Regular, null, editWindowMinutes, Guid.NewGuid());
            match.ClearDomainEvents();
            return match;
        }

        [Fact]
        public async Task Handle_ShouldCorrectScoreAndCreateAuditLog_WhenWithinEditWindow()
        {
            // Arrange
            var match = CreateFinishedMatch();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new CorrectFinishedMatchScoreCommand(match.TournamentId, match.Id, 2, 2, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(2);
            result.Value.AwayScore.Should().Be(2);
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.EntityType == "Match" && a.Action == "CorrectFinishedScore"),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldUpdateWinnerAndPublishMatchScoreCorrected_WhenCorrectionFlipsTheResult()
        {
            // Zamyka lukę z audytu: korekta zmieniała wynik, ale nie WinnerTournamentTeamId, więc
            // moduł Brackets (subskrybujący MatchScoreCorrected) nie miał czym się kierować.
            var match = CreateFinishedMatch();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new CorrectFinishedMatchScoreCommand(match.TournamentId, match.Id, 1, 3, match.AwayTournamentTeamId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<Turniejo.SharedKernel.Outbox.OutboxMessage>(m => m.Type.Contains("MatchScoreCorrected")),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenWinnerIsNeitherTeam()
        {
            var match = CreateFinishedMatch();
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new CorrectFinishedMatchScoreCommand(match.TournamentId, match.Id, 1, 1, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.WinnerTournamentTeamId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new CorrectFinishedMatchScoreCommand(Guid.NewGuid(), matchId, 1, 1, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFinished()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new CorrectFinishedMatchScoreCommand(match.TournamentId, match.Id, 1, 1, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenOutsideEditWindow()
        {
            // Arrange
            var match = CreateFinishedMatch(editWindowMinutes: -1);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            var command = new CorrectFinishedMatchScoreCommand(match.TournamentId, match.Id, 1, 1, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.EditLockedAt");
        }
    }
}
