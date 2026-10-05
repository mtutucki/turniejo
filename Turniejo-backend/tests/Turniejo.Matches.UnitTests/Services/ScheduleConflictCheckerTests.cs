using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Services
{
    public sealed class ScheduleConflictCheckerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly ScheduleConflictChecker _checker;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _pitchId = Guid.NewGuid();
        private readonly DateTimeOffset _tenOClock = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

        public ScheduleConflictCheckerTests()
        {
            _checker = new ScheduleConflictChecker(_dbContext, _tournamentModule);
            // mecz trwa 30 minut
            _tournamentModule.GetTournamentSettingsAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(_tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
            _dbContext.GetPitchByIdAsync(_pitchId, Arg.Any<CancellationToken>())
                .Returns(Pitch.Create(_pitchId, _tournamentId, "Boisko 1", null, Guid.NewGuid()).Value);
        }

        private Match NewMatch(Guid? pitchId, DateTimeOffset at, Guid? home = null, Guid? away = null)
        {
            var match = Match.Create(Guid.NewGuid(), _tournamentId, null, null, home ?? Guid.NewGuid(), away ?? Guid.NewGuid(), pitchId, at, StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        private void Existing(params Match[] matches) =>
            _dbContext.GetMatchesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(matches.ToList());

        [Fact]
        public async Task ShouldReportPitchConflict_WhenMatchesOverlapWithoutSameStartTime()
        {
            var moved = NewMatch(null, _tenOClock.AddHours(3));
            Existing(moved, NewMatch(_pitchId, _tenOClock));

            var error = await _checker.FindConflictAsync(moved, _pitchId, _tenOClock.AddMinutes(10), "PitchId", CancellationToken.None);

            error.Should().NotBeNull();
            error!.Code.Should().Be("Validation.PitchId");
        }

        [Fact]
        public async Task ShouldAllowBackToBackMatches_OnTheSamePitch()
        {
            var moved = NewMatch(null, _tenOClock.AddHours(3));
            Existing(moved, NewMatch(_pitchId, _tenOClock));

            var error = await _checker.FindConflictAsync(moved, _pitchId, _tenOClock.AddMinutes(30), "PitchId", CancellationToken.None);

            error.Should().BeNull();
        }

        [Fact]
        public async Task ShouldReportTeamConflict_WhenTeamPlaysOnAnotherPitchAtTheSameTime()
        {
            var team = Guid.NewGuid();
            var moved = NewMatch(null, _tenOClock.AddHours(3), home: team);
            Existing(moved, NewMatch(Guid.NewGuid(), _tenOClock, away: team));

            var error = await _checker.FindConflictAsync(moved, _pitchId, _tenOClock.AddMinutes(15), "PitchId", CancellationToken.None);

            error.Should().NotBeNull();
            error!.Code.Should().Be("Validation.ScheduledAt");
        }

        [Fact]
        public async Task ShouldIgnoreCancelledMatches()
        {
            var cancelled = NewMatch(_pitchId, _tenOClock);
            cancelled.Cancel("deszcz", Guid.NewGuid());
            var moved = NewMatch(null, _tenOClock.AddHours(3));
            Existing(moved, cancelled);

            var error = await _checker.FindConflictAsync(moved, _pitchId, _tenOClock, "PitchId", CancellationToken.None);

            error.Should().BeNull();
        }

        [Fact]
        public async Task ShouldNotConflictWithItself()
        {
            var match = NewMatch(_pitchId, _tenOClock);
            Existing(match);

            var error = await _checker.FindConflictAsync(match, _pitchId, _tenOClock.AddMinutes(5), "PitchId", CancellationToken.None);

            error.Should().BeNull();
        }
        [Fact]
        public async Task ShouldRejectPitchFromAnotherTournament()
        {
            var foreignPitch = Pitch.Create(Guid.NewGuid(), Guid.NewGuid(), "Obce", null, Guid.NewGuid()).Value;
            _dbContext.GetPitchByIdAsync(foreignPitch.Id, Arg.Any<CancellationToken>()).Returns(foreignPitch);
            var match = NewMatch(null, _tenOClock);
            Existing(match);

            var error = await _checker.FindConflictAsync(match, foreignPitch.Id, _tenOClock, "PitchId", CancellationToken.None);

            error!.Message.Should().Be("Pitch does not belong to this tournament");
        }

        [Fact]
        public async Task ShouldRejectMovingToInactivePitch_ButAllowKeepingCurrentOne()
        {
            var inactive = Pitch.Create(Guid.NewGuid(), _tournamentId, "Hala", null, Guid.NewGuid()).Value;
            inactive.UpdateDetails("Hala", null, false);
            _dbContext.GetPitchByIdAsync(inactive.Id, Arg.Any<CancellationToken>()).Returns(inactive);
            var elsewhere = NewMatch(null, _tenOClock);
            var onInactive = NewMatch(inactive.Id, _tenOClock.AddHours(2));
            Existing(elsewhere, onInactive);

            (await _checker.FindConflictAsync(elsewhere, inactive.Id, _tenOClock, "PitchId", CancellationToken.None))!
                .Message.Should().Be("Pitch is inactive");
            // Zmiana samej godziny meczu, który już stoi na dezaktywowanym boisku, przechodzi.
            (await _checker.FindConflictAsync(onInactive, inactive.Id, _tenOClock.AddHours(3), "PitchId", CancellationToken.None))
                .Should().BeNull();
        }

        [Theory]
        [InlineData(-60)]  // przed startem turnieju
        [InlineData(460)]  // mecz (30 min) kończyłby się po końcu turnieju o 18:00
        public async Task ShouldRejectTimeOutsideTournamentDates(int minutesFromTen)
        {
            _tournamentModule.GetTournamentAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(_tournamentId, "T", "ScheduleGenerated", "Group", Guid.NewGuid(), 8, _tenOClock, _tenOClock.AddHours(8)));
            var match = NewMatch(null, _tenOClock);
            Existing(match);

            var error = await _checker.FindConflictAsync(match, null, _tenOClock.AddMinutes(minutesFromTen), "PitchId", CancellationToken.None);

            error!.Message.Should().Be("Match must take place within the tournament dates");
        }

        [Fact]
        public async Task ShouldReportRefereeConflict_WhenMovingMatchOntoRefereesOtherMatch()
        {
            var referee = Guid.NewGuid();
            var moved = NewMatch(null, _tenOClock.AddHours(3));
            moved.AssignReferee(referee, Guid.NewGuid());
            var other = NewMatch(Guid.NewGuid(), _tenOClock);
            other.AssignReferee(referee, Guid.NewGuid());
            Existing(moved, other);

            var error = await _checker.FindConflictAsync(moved, null, _tenOClock.AddMinutes(20), "PitchId", CancellationToken.None);

            error!.Message.Should().Be("The referee already has another match at this time");
        }

        [Fact]
        public async Task FindRefereeConflict_ShouldDetectOverlap_AndIgnoreLaterMatches()
        {
            var referee = Guid.NewGuid();
            var busy = NewMatch(Guid.NewGuid(), _tenOClock.AddMinutes(15));
            busy.AssignReferee(referee, Guid.NewGuid());
            var later = NewMatch(Guid.NewGuid(), _tenOClock.AddHours(2));
            later.AssignReferee(referee, Guid.NewGuid());
            var target = NewMatch(_pitchId, _tenOClock);
            Existing(target, busy, later);

            (await _checker.FindRefereeConflictAsync(target, referee, CancellationToken.None))!.Code.Should().Be("Validation.RefereeId");
            (await _checker.FindRefereeConflictAsync(target, Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        }
    }
}
