using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.Standings.GetPublicStandings;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.UnitTests.Standings
{
    public sealed class GetPublicStandingsQueryHandlerTests
    {
        private readonly IStandingsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicStandingsQueryHandler _handler;

        public GetPublicStandingsQueryHandlerTests()
        {
            _dbContext = Substitute.For<IStandingsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicStandingsQueryHandler(_dbContext, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showStandings) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, true, true, true, showStandings, true, true));

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            // Arrange
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            // Act
            var result = await _handler.Handle(new GetPublicStandingsQuery("unknown", Guid.NewGuid()), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenShowStandingsIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showStandings: false));

            // Act
            var result = await _handler.Handle(new GetPublicStandingsQuery("cup-slug", Guid.NewGuid()), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnStandingsOrderedByPosition_WhenShowStandingsIsTrue()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showStandings: true));

            var standing = Standing.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), groupId, Guid.NewGuid()).Value;
            standing.UpdateStats(1, 1, 0, 0, 3, 0, 3);
            standing.SetPosition(1);
            _dbContext.GetStandingsByGroupIdAsync(groupId, Arg.Any<CancellationToken>()).Returns([standing]);

            // Act
            var result = await _handler.Handle(new GetPublicStandingsQuery("cup-slug", groupId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Position.Should().Be(1);
        }
    }
}
