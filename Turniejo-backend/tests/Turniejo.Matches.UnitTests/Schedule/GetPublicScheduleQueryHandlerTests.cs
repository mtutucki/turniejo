using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Schedule.GetPublicSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class GetPublicScheduleQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicScheduleQueryHandler _handler;

        public GetPublicScheduleQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicScheduleQueryHandler(_dbContext, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showSchedule) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, true, true, showSchedule, true, true, true, true));

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            // Arrange
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            // Act
            var result = await _handler.Handle(new GetPublicScheduleQuery("unknown"), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenShowScheduleIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showSchedule: false));

            // Act
            var result = await _handler.Handle(new GetPublicScheduleQuery("cup-slug"), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnMatches_OrderedByScheduledAt_WhenShowScheduleIsTrue()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showSchedule: true));

            var laterMatch = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(2), StageType.Group).Value;
            var earlierMatch = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([laterMatch, earlierMatch]);

            // Act
            var result = await _handler.Handle(new GetPublicScheduleQuery("cup-slug"), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Id.Should().Be(earlierMatch.Id);
            result.Value[1].Id.Should().Be(laterMatch.Id);
        }

        [Fact]
        public async Task Handle_ShouldNeverExposeRefereeId_EvenWhenMatchHasOne()
        {
            // Audyt: kto sędziuje to wewnętrzna informacja organizacyjna, nie coś do publicznego
            // ujawnienia — nawet dla meczów, które się jeszcze nie odbyły.
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showSchedule: true));

            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.AssignReferee(Guid.NewGuid(), Guid.NewGuid());
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([match]);

            var result = await _handler.Handle(new GetPublicScheduleQuery("cup-slug"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle().Which.RefereeId.Should().BeNull();
        }
            // QA v3: PUB-003c / PUB-003f - przełączniki „Terminarz” i „Mecze na żywo” działają niezależnie.
        private (Match Live, Match Upcoming) ArrangeLiveAndUpcoming(Guid tournamentId, bool showSchedule, bool showLive)
        {
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(new PublicTournamentDto(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                    new TournamentPublicSettingsDto(tournamentId, true, true, true, showSchedule, true, true, showLive, true)));
            var live = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, StageType.Group).Value;
            live.Start(Guid.NewGuid());
            live.UpdateScore(1, 0);
            var upcoming = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddHours(2), StageType.Group).Value;
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns([live, upcoming]);
            return (live, upcoming);
        }

        [Fact]
        public async Task PUB_003c_ScheduleOff_LiveOn_ReturnsOnlyLiveMatches()
        {
            var (live, _) = ArrangeLiveAndUpcoming(Guid.NewGuid(), showSchedule: false, showLive: true);

            var result = await _handler.Handle(new GetPublicScheduleQuery("cup-slug"), CancellationToken.None);

            result.Value.Select(m => m.Id).Should().Equal(live.Id);
        }

        [Fact]
        public async Task PUB_003f_LiveOff_HidesTheScoreOfMatchesInProgress()
        {
            var (live, _) = ArrangeLiveAndUpcoming(Guid.NewGuid(), showSchedule: true, showLive: false);

            var result = await _handler.Handle(new GetPublicScheduleQuery("cup-slug"), CancellationToken.None);

            var dto = result.Value.Single(m => m.Id == live.Id);
            (dto.HomeScore, dto.AwayScore).Should().Be((0, 0));
        }
    }
}
