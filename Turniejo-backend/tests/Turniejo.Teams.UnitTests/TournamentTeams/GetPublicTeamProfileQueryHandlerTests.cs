using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.GetPublicTeamProfile;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetPublicTeamProfileQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicTeamProfileQueryHandler _handler;

        public GetPublicTeamProfileQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicTeamProfileQueryHandler(_dbContext, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showTeams) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, showTeams, true, true, true, true, true, true));

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            var result = await _handler.Handle(new GetPublicTeamProfileQuery("unknown", Guid.NewGuid()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShowTeamsIsFalse()
        {
            // Zamyka lukę z audytu: brakujący publiczny profil pojedynczej drużyny.
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showTeams: false));

            var result = await _handler.Handle(new GetPublicTeamProfileQuery("cup-slug", Guid.NewGuid()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamBelongsToDifferentTournament()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showTeams: true));

            var teamFromOtherTournament = TournamentTeam.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(teamFromOtherTournament.Id, Arg.Any<CancellationToken>()).Returns(teamFromOtherTournament);

            var result = await _handler.Handle(new GetPublicTeamProfileQuery("cup-slug", teamFromOtherTournament.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamIsNotAccepted()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showTeams: true));

            var pendingTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            _dbContext.GetTournamentTeamByIdAsync(pendingTeam.Id, Arg.Any<CancellationToken>()).Returns(pendingTeam);

            var result = await _handler.Handle(new GetPublicTeamProfileQuery("cup-slug", pendingTeam.Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnProfile_WhenTeamIsAcceptedAndPublic()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showTeams: true));

            var acceptedTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()).Value;
            acceptedTeam.ChangeRegistrationStatus(RegistrationStatus.Accepted, Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(acceptedTeam.Id, Arg.Any<CancellationToken>()).Returns(acceptedTeam);

            var team = Team.Create(acceptedTeam.TeamId, Guid.NewGuid(), Guid.NewGuid(), "Lions", "LIO", null).Value;
            _dbContext.GetTeamByIdAsync(acceptedTeam.TeamId, Arg.Any<CancellationToken>()).Returns(team);

            var result = await _handler.Handle(new GetPublicTeamProfileQuery("cup-slug", acceptedTeam.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentTeamId.Should().Be(acceptedTeam.Id);
            result.Value.Name.Should().Be("Lions");
        }
    }
}
