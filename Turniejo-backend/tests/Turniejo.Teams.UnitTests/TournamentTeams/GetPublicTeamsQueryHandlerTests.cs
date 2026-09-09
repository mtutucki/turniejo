using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.GetPublicTeams;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetPublicTeamsQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicTeamsQueryHandler _handler;

        public GetPublicTeamsQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicTeamsQueryHandler(_dbContext, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showTeams) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, showTeams, true, true, true, true, true, true));

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            // Arrange
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            // Act
            var result = await _handler.Handle(new GetPublicTeamsQuery("unknown"), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenShowTeamsIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showTeams: false));

            // Act
            var result = await _handler.Handle(new GetPublicTeamsQuery("cup-slug"), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnOnlyAcceptedTeams_WithNames()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showTeams: true));

            var acceptedTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            acceptedTeam.ChangeRegistrationStatus(Turniejo.Teams.Domain.Enums.RegistrationStatus.Accepted, Guid.NewGuid());
            var pendingTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;

            _dbContext.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns([acceptedTeam, pendingTeam]);

            var team = Team.Create(acceptedTeam.TeamId, Guid.NewGuid(), Guid.NewGuid(), "Lions", "LIO", null).Value;
            _dbContext.GetTeamByIdAsync(acceptedTeam.TeamId, Arg.Any<CancellationToken>()).Returns(team);

            // Act
            var result = await _handler.Handle(new GetPublicTeamsQuery("cup-slug"), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].TournamentTeamId.Should().Be(acceptedTeam.Id);
            result.Value[0].Name.Should().Be("Lions");
        }
    }
}
