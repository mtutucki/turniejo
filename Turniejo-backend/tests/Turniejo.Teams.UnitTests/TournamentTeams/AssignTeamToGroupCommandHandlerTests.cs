using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.TournamentTeams.AssignTeamToGroup;
using Turniejo.Teams.Domain.Entities;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class AssignTeamToGroupCommandHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly AssignTeamToGroupCommandHandler _handler;

        public AssignTeamToGroupCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new AssignTeamToGroupCommandHandler(_dbContext, _tournamentModule);
            // Domyślnie turniej przed terminarzem: grupy można zmieniać.
            _tournamentModule.GetTournamentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => new TournamentDto(call.Arg<Guid>(), "Cup", "RegistrationClosed", "Group", Guid.NewGuid(), 16));
        }

        private static TournamentTeam CreateFakeTournamentTeam(Guid tournamentId, Guid categoryId)
        {
            var tournamentTeam = TournamentTeam.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), categoryId, Guid.NewGuid()).Value;
            tournamentTeam.ClearDomainEvents();
            return tournamentTeam;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenGroupExistsInSameTournamentAndCategory()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId, categoryId);
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new TournamentGroupDto(groupId, tournamentId, categoryId, "Grupa A"));
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeam.Id, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.GroupId.Should().Be(groupId);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenUnassigningFromGroup()
        {
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId, Guid.NewGuid());
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeam.Id, null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            tournamentTeam.GroupId.Should().BeNull();
            await _tournamentModule.DidNotReceive().GetTournamentGroupAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamNotFound()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeam?)null);
            var command = new AssignTeamToGroupCommand(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupDoesNotExist()
        {
            // Zamyka lukę z audytu: GroupId nie ma FK, więc dało się przypisać drużynę do
            // nieistniejącego GUID-a grupy — drużyna cicho znikała z fazy grupowej.
            var tournamentId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId, Guid.NewGuid());
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>()).Returns((TournamentGroupDto?)null);
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeam.Id, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentGroup.NotFound");
            tournamentTeam.GroupId.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupBelongsToAnotherTournament()
        {
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId, categoryId);
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new TournamentGroupDto(groupId, Guid.NewGuid(), categoryId, "Grupa z innego turnieju"));
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeam.Id, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.GroupId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupBelongsToAnotherCategory()
        {
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var tournamentTeam = CreateFakeTournamentTeam(tournamentId, categoryId);
            var groupId = Guid.NewGuid();
            _dbContext.GetTournamentTeamByIdAsync(tournamentTeam.Id, Arg.Any<CancellationToken>()).Returns(tournamentTeam);
            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new TournamentGroupDto(groupId, tournamentId, Guid.NewGuid(), "Grupa z innej kategorii"));
            var command = new AssignTeamToGroupCommand(tournamentId, tournamentTeam.Id, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.GroupId");
        }
    }
}
