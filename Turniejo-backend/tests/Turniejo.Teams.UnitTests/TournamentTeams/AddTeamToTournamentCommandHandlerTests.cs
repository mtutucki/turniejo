using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.AddTeamToTournament;
using Turniejo.Teams.Domain.Entities;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class AddTeamToTournamentCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddTeamToTournamentCommandHandler _handler;

        public AddTeamToTournamentCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new AddTeamToTournamentCommandHandler(_dbContext, _tournamentModule, _currentUserContext);
        }

        private static Team CreateFakeTeam(Guid organizationId)
        {
            var team = Team.Create(Guid.NewGuid(), organizationId, Guid.NewGuid(), "FC Testowy", null, null).Value;
            team.ClearDomainEvents();
            return team;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var team = CreateFakeTeam(organizationId);
            var categoryId = Guid.NewGuid();

            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", null, organizationId, TeamLimit: 16));
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            _dbContext.TournamentTeamExistsAsync(tournamentId, team.Id, categoryId, Arg.Any<CancellationToken>()).Returns(false);
            _dbContext.CountAcceptedTournamentTeamsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(0);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            var command = new AddTeamToTournamentCommand(tournamentId, team.Id, categoryId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddTournamentTeamAsync(Arg.Any<TournamentTeam>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns((TournamentDto?)null);
            var command = new AddTeamToTournamentCommand(tournamentId, Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", null, Guid.NewGuid(), TeamLimit: 16));
            _dbContext.GetTeamByIdAsync(teamId, Arg.Any<CancellationToken>()).Returns((Team?)null);
            var command = new AddTeamToTournamentCommand(tournamentId, teamId, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Team.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamBelongsToDifferentOrganization()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var team = CreateFakeTeam(Guid.NewGuid());
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", null, Guid.NewGuid(), TeamLimit: 16));
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            var command = new AddTeamToTournamentCommand(tournamentId, team.Id, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TeamId");
        }

        /// <summary>Istniejący wpis drużyny w turnieju w danym statusie zgłoszenia.</summary>
        private TournamentTeam ExistingRegistration(Guid tournamentId, Guid teamId, Guid categoryId, RegistrationStatus status)
        {
            var registration = TournamentTeam.Create(Guid.NewGuid(), tournamentId, teamId, categoryId, Guid.NewGuid()).Value;
            if (status is RegistrationStatus.Accepted or RegistrationStatus.Withdrawn)
                registration.ChangeRegistrationStatus(RegistrationStatus.Accepted, Guid.NewGuid());
            if (status == RegistrationStatus.Withdrawn)
                registration.ChangeRegistrationStatus(RegistrationStatus.Withdrawn, Guid.NewGuid());
            if (status == RegistrationStatus.Rejected)
                registration.ChangeRegistrationStatus(RegistrationStatus.Rejected, Guid.NewGuid());
            registration.ClearDomainEvents();
            _dbContext.GetTournamentTeamForUpdateAsync(tournamentId, teamId, categoryId, Arg.Any<CancellationToken>())
                .Returns(registration);
            return registration;
        }

        [Theory]
        [InlineData(RegistrationStatus.Pending)]
        [InlineData(RegistrationStatus.Accepted)]
        public async Task Handle_ShouldFail_WhenAlreadyRegistered(RegistrationStatus status)
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var team = CreateFakeTeam(organizationId);
            var categoryId = Guid.NewGuid();

            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", null, organizationId, TeamLimit: 16));
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            ExistingRegistration(tournamentId, team.Id, categoryId, status);

            var command = new AddTeamToTournamentCommand(tournamentId, team.Id, categoryId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TeamId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(RegistrationStatus.Rejected)]
        [InlineData(RegistrationStatus.Withdrawn)]
        public async Task Handle_ShouldReapply_RejectedOrWithdrawnTeam(RegistrationStatus status)
        {
            // Odrzucona drużyna nie była już do przywrócenia — ponowne zgłoszenie wznawia jej wpis.
            var organizationId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var team = CreateFakeTeam(organizationId);
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", null, organizationId, TeamLimit: 16));
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var registration = ExistingRegistration(tournamentId, team.Id, categoryId, status);

            var result = await _handler.Handle(new AddTeamToTournamentCommand(tournamentId, team.Id, categoryId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentTeamId.Should().Be(registration.Id);
            registration.RegistrationStatus.Should().Be(RegistrationStatus.Pending);
            await _dbContext.DidNotReceive().AddTournamentTeamAsync(Arg.Any<TournamentTeam>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTeamLimitReached()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var team = CreateFakeTeam(organizationId);
            var categoryId = Guid.NewGuid();

            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", null, organizationId, TeamLimit: 2));
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            _dbContext.TournamentTeamExistsAsync(tournamentId, team.Id, categoryId, Arg.Any<CancellationToken>()).Returns(false);
            _dbContext.CountAcceptedTournamentTeamsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(2);

            var command = new AddTeamToTournamentCommand(tournamentId, team.Id, categoryId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TeamId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(0, false)]
        public async Task Handle_ShouldAssignToTheOnlyGroupOfCategory(int groupsInCategory, bool assigned)
        {
            // Kategoria z jedną grupą (domyślna "Grupa A") — drużyna od razu w niej; przy kilku grupach nie.
            var organizationId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var team = CreateFakeTeam(organizationId);
            var categoryId = Guid.NewGuid();
            var groups = Enumerable.Range(0, groupsInCategory)
                .Select(i => new TournamentGroupDto(Guid.NewGuid(), tournamentId, categoryId, $"Grupa {i}"))
                .Append(new TournamentGroupDto(Guid.NewGuid(), tournamentId, Guid.NewGuid(), "Inna kategoria"))
                .ToList();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", "RegistrationOpen", "Group", organizationId, TeamLimit: 16));
            _tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(groups);
            _dbContext.GetTeamByIdAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            TournamentTeam? added = null;
            await _dbContext.AddTournamentTeamAsync(Arg.Do<TournamentTeam>(t => added = t), Arg.Any<CancellationToken>());

            var result = await _handler.Handle(new AddTeamToTournamentCommand(tournamentId, team.Id, categoryId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            if (assigned)
                added!.GroupId.Should().Be(groups[0].Id);
            else
                added!.GroupId.Should().BeNull();
        }
    }
}
