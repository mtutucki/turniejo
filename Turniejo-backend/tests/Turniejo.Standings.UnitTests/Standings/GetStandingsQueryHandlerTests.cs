using FluentAssertions;
using NSubstitute;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.Standings.GetStandings;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.UnitTests.Standings
{
    public sealed class GetStandingsQueryHandlerTests
    {
        private readonly IStandingsDbContext _dbContext;
        private readonly GetStandingsQueryHandler _handler;

        public GetStandingsQueryHandlerTests()
        {
            _dbContext = Substitute.For<IStandingsDbContext>();
            _handler = new GetStandingsQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnStandingsOrderedByPosition()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            var second = Standing.Create(Guid.NewGuid(), tournamentId, categoryId, groupId, Guid.NewGuid()).Value;
            second.SetPosition(2);
            var first = Standing.Create(Guid.NewGuid(), tournamentId, categoryId, groupId, Guid.NewGuid()).Value;
            first.SetPosition(1);

            _dbContext.GetStandingsByGroupIdAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new List<Standing> { first, second });

            // Act
            var result = await _handler.Handle(new GetStandingsQuery(tournamentId, groupId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Position.Should().Be(1);
            result.Value[1].Position.Should().Be(2);
        }

        [Fact]
        public async Task Handle_ShouldExcludeStandings_FromADifferentTournament()
        {
            // Audyt: autoryzacja sprawdza tylko rolę na request.TournamentId — bez filtrowania po
            // TournamentId w samym query, organizator turnieju A podający GroupId z turnieju B
            // dostałby jego tabelę.
            var tournamentId = Guid.NewGuid();
            var otherTournamentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            var foreignStanding = Standing.Create(Guid.NewGuid(), otherTournamentId, Guid.NewGuid(), groupId, Guid.NewGuid()).Value;
            _dbContext.GetStandingsByGroupIdAsync(groupId, Arg.Any<CancellationToken>()).Returns([foreignStanding]);

            var result = await _handler.Handle(new GetStandingsQuery(tournamentId, groupId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
