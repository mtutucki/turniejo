using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Teams.Application.Abstractions;
using Turniejo.Teams.Application.Services;
using Turniejo.Teams.Application.TournamentTeams.GetTeamSchedule;

namespace Turniejo.Teams.UnitTests.TournamentTeams
{
    public sealed class GetTeamScheduleQueryHandlerTests
    {
        private readonly ITeamsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly GetTeamScheduleQueryHandler _handler;

        public GetTeamScheduleQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITeamsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            var resolver = new TeamMatchResolver(_dbContext, _matchModule);
            _handler = new GetTeamScheduleQueryHandler(resolver);

            _dbContext.GetTournamentTeamByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Turniejo.Teams.Domain.Entities.TournamentTeam?)null);
        }

        [Fact]
        public async Task Handle_ShouldReturnOnlyNonFinishedMatches_OrderedByScheduledAt()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var matches = new List<TournamentMatchDto>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), "Scheduled", DateTimeOffset.UtcNow.AddDays(3), tournamentTeamId, Guid.NewGuid(), 0, 0),
                new(Guid.NewGuid(), Guid.NewGuid(), "Finished", DateTimeOffset.UtcNow.AddDays(-1), tournamentTeamId, Guid.NewGuid(), 1, 0),
                new(Guid.NewGuid(), Guid.NewGuid(), "Ready", DateTimeOffset.UtcNow.AddDays(1), tournamentTeamId, Guid.NewGuid(), 0, 0),
            };
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(matches);

            // Act
            var result = await _handler.Handle(new GetTeamScheduleQuery(tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Status.Should().Be("Ready");
            result.Value[1].Status.Should().Be("Scheduled");
        }

        [Fact]
        public async Task Handle_ShouldFallBackToUnknown_WhenTeamLookupMisses()
        {
            // TeamMatchResolver nie ma własnego pliku testowego. Konstruktor mockuje
            // GetTournamentTeamByIdAsync na null dla każdego ID, więc to jest ścieżka fallbacku, nie
            // "domyślne" zachowanie — dotąd żaden test tego jawnie nie asertował.
            var tournamentTeamId = Guid.NewGuid();
            var match = new TournamentMatchDto(Guid.NewGuid(), Guid.NewGuid(), "Scheduled", DateTimeOffset.UtcNow.AddDays(1), tournamentTeamId, Guid.NewGuid(), 0, 0);
            _matchModule.GetMatchesByTournamentTeamIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([match]);

            // Act
            var result = await _handler.Handle(new GetTeamScheduleQuery(tournamentTeamId), CancellationToken.None);

            // Assert
            result.Value[0].HomeTeamName.Should().Be("Unknown");
            result.Value[0].AwayTeamName.Should().Be("Unknown");
        }
    }
}
