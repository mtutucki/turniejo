using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Application.Matches.RescheduleMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class RescheduleMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly RescheduleMatchCommandHandler _handler;

        public RescheduleMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new RescheduleMatchCommandHandler(_dbContext, new ScheduleConflictChecker(_dbContext, Substitute.For<ITournamentModule>(), Substitute.For<ITeamModule>()), new PlannedSlotShifter(_dbContext, Substitute.For<ITournamentModule>(), Substitute.For<IBracketModule>()), _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId, Guid pitchId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        private static Match OtherMatch(Guid tournamentId, Guid? pitchId, DateTimeOffset scheduledAt)
        {
            var other = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, scheduledAt, StageType.Group).Value;
            other.ClearDomainEvents();
            return other;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId, pitchId);
            var newTime = DateTimeOffset.UtcNow.AddDays(3);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new RescheduleMatchCommand(tournamentId, match.Id, newTime);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.ScheduledAt.Should().Be(newTime);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new RescheduleMatchCommand(Guid.NewGuid(), matchId, DateTimeOffset.UtcNow);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPitchTimeConflict()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId, pitchId);
            var newTime = DateTimeOffset.UtcNow.AddDays(3);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match> { match, OtherMatch(tournamentId, pitchId, newTime) });
            var command = new RescheduleMatchCommand(tournamentId, match.Id, newTime);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ScheduledAt");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
