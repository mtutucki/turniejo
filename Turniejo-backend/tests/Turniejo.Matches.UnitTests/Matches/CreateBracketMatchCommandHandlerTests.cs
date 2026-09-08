using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.CreateBracketMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class CreateBracketMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly CreateBracketMatchCommandHandler _handler;

        public CreateBracketMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new CreateBracketMatchCommandHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldCreateAndPersistMatch_WhenDataIsValid()
        {
            // Zamyka lukę z audytu testów: jedyny handler w backendzie bez żadnego pokrycia, mimo że
            // leży na hot path całego mechanizmu awansu w drabince (BracketAdvancer.TryCreateMatchAsync).
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var homeTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();
            var command = new CreateBracketMatchCommand(tournamentId, categoryId, homeTeamId, awayTeamId, StageType.Knockout);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddMatchAsync(
                Arg.Is<Match>(m =>
                    m.TournamentId == tournamentId && m.CategoryId == categoryId &&
                    m.HomeTournamentTeamId == homeTeamId && m.AwayTournamentTeamId == awayTeamId &&
                    m.StageType == StageType.Knockout && m.Status == MatchStatus.Scheduled),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnCreatedMatchId()
        {
            var command = new CreateBracketMatchCommand(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), StageType.Final);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenHomeAndAwayTeamsAreTheSame()
        {
            // Match.Create odrzuca drużynę graną samą przeciwko sobie — handler musi to poprawnie
            // propagować jako Result.Failure, nie zapisywać nic do bazy.
            var teamId = Guid.NewGuid();
            var command = new CreateBracketMatchCommand(Guid.NewGuid(), Guid.NewGuid(), teamId, teamId, StageType.Knockout);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.AwayTournamentTeamId");
            await _dbContext.DidNotReceive().AddMatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
