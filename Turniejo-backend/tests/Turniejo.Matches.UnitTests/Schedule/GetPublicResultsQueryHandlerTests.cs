using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Schedule.GetPublicResults;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class GetPublicResultsQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicResultsQueryHandler _handler;

        public GetPublicResultsQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicResultsQueryHandler(_dbContext, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showSchedule) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, true, true, showSchedule, true, true, true, true));

        [Fact]
        public async Task Handle_ShouldReturnOnlyFinishedMatches()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showSchedule: true));

            var scheduledMatch = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

            var finishedMatch = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(-1), StageType.Group).Value;
            finishedMatch.Start(Guid.NewGuid());
            finishedMatch.Finish(MatchResultType.Regular, finishedMatch.HomeTournamentTeamId, 60, Guid.NewGuid());

            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([scheduledMatch, finishedMatch]);

            // Act
            var result = await _handler.Handle(new GetPublicResultsQuery("cup-slug"), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(finishedMatch.Id);
            result.Value[0].Status.Should().Be("Finished");
        }

        [Fact]
        public async Task Handle_ShouldNeverExposeRefereeId_EvenWhenMatchHasOne()
        {
            // Audyt: kto sędziuje to wewnętrzna informacja organizacyjna, nie coś do publicznego
            // ujawnienia przez stronę turnieju.
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showSchedule: true));

            var finishedMatch = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(-1), StageType.Group).Value;
            finishedMatch.AssignReferee(Guid.NewGuid(), Guid.NewGuid());
            finishedMatch.Start(Guid.NewGuid());
            finishedMatch.Finish(MatchResultType.Regular, finishedMatch.HomeTournamentTeamId, 60, Guid.NewGuid());
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([finishedMatch]);

            var result = await _handler.Handle(new GetPublicResultsQuery("cup-slug"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle().Which.RefereeId.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotPublic()
        {
            // Arrange
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            // Act
            var result = await _handler.Handle(new GetPublicResultsQuery("unknown"), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }
    }
}
