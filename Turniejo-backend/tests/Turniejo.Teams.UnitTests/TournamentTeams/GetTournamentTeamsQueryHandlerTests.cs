using FluentAssertions;
using NSubstitute;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.GetTournamentTeams;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetTournamentTeamsQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly GetTournamentTeamsQueryHandler _handler;

        public GetTournamentTeamsQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _handler = new GetTournamentTeamsQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenTournamentTeamsExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeam> { tournamentTeam });

            // Act
            var result = await _handler.Handle(new GetTournamentTeamsQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(tournamentTeam.Id);
            result.Value[0].RegistrationStatus.Should().Be(tournamentTeam.RegistrationStatus.ToString());
        }

        [Fact]
        public async Task Handle_ShouldIncludeTeamDetails_WhenTeamExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var team = Team.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Orły Warszawa", "ORŁ", "https://example.com/logo.png").Value;
            var tournamentTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, team.Id, Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeam> { tournamentTeam });
            _dbContext.GetTeamsByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Team> { team });

            // Act
            var result = await _handler.Handle(new GetTournamentTeamsQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value[0].TeamName.Should().Be("Orły Warszawa");
            result.Value[0].TeamShortName.Should().Be("ORŁ");
            result.Value[0].TeamLogoUrl.Should().Be("https://example.com/logo.png");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoTournamentTeamsExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeam>());

            // Act
            var result = await _handler.Handle(new GetTournamentTeamsQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
