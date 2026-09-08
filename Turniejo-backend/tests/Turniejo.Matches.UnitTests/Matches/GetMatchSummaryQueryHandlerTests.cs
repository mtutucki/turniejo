using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchSummary;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchSummaryQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly GetMatchSummaryQueryHandler _handler;

        public GetMatchSummaryQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _handler = new GetMatchSummaryQueryHandler(_dbContext, _teamModule);
        }

        [Fact]
        public async Task Handle_ShouldReturnSummaryWithTeamNames_WhenMatchExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var homeTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            _teamModule.GetTournamentTeamAsync(match.HomeTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(match.HomeTournamentTeamId, tournamentId, homeTeamId, Guid.NewGuid(), null, "Accepted"));
            _teamModule.GetTournamentTeamAsync(match.AwayTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(match.AwayTournamentTeamId, tournamentId, awayTeamId, Guid.NewGuid(), null, "Accepted"));
            _teamModule.GetTeamAsync(homeTeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(homeTeamId, "Lions", "LIO"));
            _teamModule.GetTeamAsync(awayTeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(awayTeamId, "Tigers", "TIG"));

            // Act
            var result = await _handler.Handle(new GetMatchSummaryQuery(tournamentId, match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.HomeTeamName.Should().Be("Lions");
            result.Value.AwayTeamName.Should().Be("Tigers");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchSummaryQuery(Guid.NewGuid(), matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }
    }
}
