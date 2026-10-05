using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.Standings.RecalculateStandings;
using Turniejo.Standings.Domain.Entities;

namespace Turniejo.Standings.UnitTests.Standings
{
    public sealed class RecalculateStandingsCommandHandlerTests
    {
        private readonly IStandingsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly ITeamModule _teamModule;
        private readonly IMatchModule _matchModule;
        private readonly RecalculateStandingsCommandHandler _handler;

        public RecalculateStandingsCommandHandlerTests()
        {
            _dbContext = Substitute.For<IStandingsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _matchModule = Substitute.For<IMatchModule>();
            _handler = new RecalculateStandingsCommandHandler(_dbContext, _tournamentModule, _teamModule, _matchModule);

            _dbContext.GetStandingAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Standing?)null);
            _tournamentModule.GetTieBreakerRulesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<TieBreakerRuleDto>());
        }

        [Fact]
        public async Task Handle_ShouldCreateStandings_ForAcceptedTeamsInGroup_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var teamA = Guid.NewGuid();
            var teamB = Guid.NewGuid();

            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new TournamentGroupDto(groupId, tournamentId, categoryId, "Group A"));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
            _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeamDto>
                {
                    new(teamA, tournamentId, Guid.NewGuid(), categoryId, groupId, "Accepted"),
                    new(teamB, tournamentId, Guid.NewGuid(), categoryId, groupId, "Accepted"),
                    new(Guid.NewGuid(), tournamentId, Guid.NewGuid(), categoryId, Guid.NewGuid(), "Accepted") // different group
                });
            _matchModule.GetFinishedMatchesByGroupIdAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new List<GroupMatchDto> { new(Guid.NewGuid(), groupId, teamA, teamB, 2, 1) });

            var command = new RecalculateStandingsCommand(groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TeamCount.Should().Be(2);
            await _dbContext.Received(2).AddStandingAsync(Arg.Any<Standing>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupNotFound()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>()).Returns((TournamentGroupDto?)null);

            // Act
            var result = await _handler.Handle(new RecalculateStandingsCommand(groupId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentGroup.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenSettingsNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new TournamentGroupDto(groupId, tournamentId, Guid.NewGuid(), "Group A"));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns((TournamentSettingsSummaryDto?)null);

            // Act
            var result = await _handler.Handle(new RecalculateStandingsCommand(groupId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentSettings.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldUpdateExistingStanding_InsteadOfCreatingDuplicate()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var teamA = Guid.NewGuid();
            var existingStanding = Standing.Create(Guid.NewGuid(), tournamentId, categoryId, groupId, teamA).Value;

            _tournamentModule.GetTournamentGroupAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new TournamentGroupDto(groupId, tournamentId, categoryId, "Group A"));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
            _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeamDto> { new(teamA, tournamentId, Guid.NewGuid(), categoryId, groupId, "Accepted") });
            _matchModule.GetFinishedMatchesByGroupIdAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new List<GroupMatchDto>());
            _dbContext.GetStandingAsync(groupId, teamA, Arg.Any<CancellationToken>()).Returns(existingStanding);

            // Act
            var result = await _handler.Handle(new RecalculateStandingsCommand(groupId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.DidNotReceive().AddStandingAsync(Arg.Any<Standing>(), Arg.Any<CancellationToken>());
            existingStanding.Position.Should().Be(1);
        }
    }
}
