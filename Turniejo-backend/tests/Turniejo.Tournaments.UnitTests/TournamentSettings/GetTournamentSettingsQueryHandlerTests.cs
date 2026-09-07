using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentSetting.GetTournamentSettings;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentSettings
{
    public sealed class GetTournamentSettingsQueryHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly GetTournamentSettingsQueryHandler _handler;

        public GetTournamentSettingsQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _handler = new GetTournamentSettingsQueryHandler(_dbContext);
        }

        private static Tournament CreateFakeTournament() =>
        Tournament.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
            8, "Opis testowego turnieju").Value;

        [Fact]
        public async Task Handle_ShouldReturnMappedDto_WhenTournamentExist()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);

            //Act
            var result = await _handler.Handle(new GetTournamentSettingsQuery(tournament.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value.TournamentId.Should().Be(tournament.Id);
            result.Value.PointsForWin.Should().Be(tournament.Settings.PointsForWin);
            result.Value.PointsForDraw.Should().Be(tournament.Settings.PointsForDraw);
            result.Value.PointsForLoss.Should().Be(tournament.Settings.PointsForLoss);
            result.Value.MatchDurationMinutes.Should().Be(tournament.Settings.MatchDurationMinutes);
            result.Value.BreakDurationMinutes.Should().Be(tournament.Settings.BreakDurationMinutes);
            result.Value.IsThirdPlaceMatchEnabled.Should().Be(tournament.Settings.IsThirdPlaceMatchEnabled);
            result.Value.RefereeEditWindowMinutes.Should().Be(tournament.Settings.RefereeEditWindowMinutes);
            result.Value.WalkoverHomeScore.Should().Be(tournament.Settings.WalkoverHomeScore);
            result.Value.WalkoverAwayScore.Should().Be(tournament.Settings.WalkoverAwayScore);
            result.Value.CanCoachEditSquadAfterStart.Should().Be(tournament.Settings.CanCoachEditSquadAfterStart);
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenTournamentNotExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns((Tournament?)null);
            // Act
            var result = await _handler.Handle(new GetTournamentSettingsQuery(tournamentId), CancellationToken.None);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }
    }
}
