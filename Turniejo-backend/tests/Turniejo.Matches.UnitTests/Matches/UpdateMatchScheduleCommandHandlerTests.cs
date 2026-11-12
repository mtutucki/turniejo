using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Application.Matches.UpdateMatchSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class UpdateMatchScheduleCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly UpdateMatchScheduleCommandHandler _handler;

        public UpdateMatchScheduleCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new UpdateMatchScheduleCommandHandler(_dbContext, new ScheduleConflictChecker(_dbContext, _tournamentModule, Substitute.For<ITeamModule>()), new PlannedSlotShifter(_dbContext, Substitute.For<ITournamentModule>(), Substitute.For<IBracketModule>()), new PitchRefereeAssigner(_dbContext, Substitute.For<IUserModule>(), Substitute.For<IAuthorizationService>(), Substitute.For<ITournamentModule>()), _currentUserContext);
        }

        private static Match CreateFakeMatch(Guid tournamentId)
        {
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        private static Match OtherMatch(Guid tournamentId, Guid? pitchId, DateTimeOffset scheduledAt)
        {
            var other = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, scheduledAt, StageType.Group).Value;
            other.ClearDomainEvents();
            return other;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var newPitchId = Guid.NewGuid();
            var newTime = DateTimeOffset.UtcNow.AddDays(2);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetPitchByIdAsync(newPitchId, Arg.Any<CancellationToken>())
                .Returns(Pitch.Create(newPitchId, tournamentId, "Boisko 2", null, Guid.NewGuid()).Value);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new UpdateMatchScheduleCommand(tournamentId, match.Id, newPitchId, newTime);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            match.PitchId.Should().Be(newPitchId);
            match.ScheduledAt.Should().Be(newTime);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);
            var command = new UpdateMatchScheduleCommand(Guid.NewGuid(), matchId, Guid.NewGuid(), DateTimeOffset.UtcNow);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPitchTimeConflict()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var match = CreateFakeMatch(tournamentId);
            var newPitchId = Guid.NewGuid();
            var newTime = DateTimeOffset.UtcNow.AddDays(2);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match> { match, OtherMatch(tournamentId, newPitchId, newTime) });
            var command = new UpdateMatchScheduleCommand(tournamentId, match.Id, newPitchId, newTime);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PitchId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        private void ShiftScenario(Guid tournamentId, params Match[] matches)
        {
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(matches.ToList());
            _dbContext.GetTrackedMatchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(ci => matches.Where(m => ci.Arg<IReadOnlyCollection<Guid>>().Contains(m.Id)).ToList());
            _dbContext.ExecuteInTransactionAsync(Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
                .Returns(ci => ci.Arg<Func<Task>>()());
        }

        [Fact]
        public async Task Handle_ShiftFollowing_MovesLaterMatchesOnTheSamePitchOnly()
        {
            var tournamentId = Guid.NewGuid();
            var pitch = Guid.NewGuid();
            var otherPitch = Guid.NewGuid();
            var ten = DateTimeOffset.UtcNow.AddDays(1);
            var match = OtherMatch(tournamentId, pitch, ten);
            var earlier = OtherMatch(tournamentId, pitch, ten.AddMinutes(-30));
            var next = OtherMatch(tournamentId, pitch, ten.AddMinutes(30));
            var last = OtherMatch(tournamentId, pitch, ten.AddMinutes(60));
            var elsewhere = OtherMatch(tournamentId, otherPitch, ten.AddMinutes(30));
            ShiftScenario(tournamentId, match, earlier, next, last, elsewhere);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(
                new UpdateMatchScheduleCommand(tournamentId, match.Id, pitch, ten.AddMinutes(15), ShiftFollowing: true), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.ScheduledAt.Should().Be(ten.AddMinutes(15));
            next.ScheduledAt.Should().Be(ten.AddMinutes(45));
            last.ScheduledAt.Should().Be(ten.AddMinutes(75));
            earlier.ScheduledAt.Should().Be(ten.AddMinutes(-30));
            elsewhere.ScheduledAt.Should().Be(ten.AddMinutes(30));
        }

        [Fact]
        public async Task Handle_ShiftFollowing_LeavesAMatchThatAlreadyStarted()
        {
            var tournamentId = Guid.NewGuid();
            var pitch = Guid.NewGuid();
            var ten = DateTimeOffset.UtcNow.AddDays(1);
            var match = OtherMatch(tournamentId, pitch, ten);
            var live = OtherMatch(tournamentId, pitch, ten.AddMinutes(30));
            live.Start(Guid.NewGuid());
            ShiftScenario(tournamentId, match, live);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(
                new UpdateMatchScheduleCommand(tournamentId, match.Id, pitch, ten.AddMinutes(10), ShiftFollowing: true), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            live.ScheduledAt.Should().Be(ten.AddMinutes(30));
        }

        [Fact]
        public async Task Handle_ShiftFollowing_AsksFirst_WhenAShiftedMatchHitsATeamOnAnotherPitch()
        {
            var sc = OtherPitchScenario();

            var result = await _handler.Handle(
                new UpdateMatchScheduleCommand(sc.TournamentId, sc.Match.Id, sc.Pitch, sc.Ten.AddMinutes(10), ShiftFollowing: true), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.OtherPitches");
            sc.Match.ScheduledAt.Should().Be(sc.Ten);
            sc.Elsewhere.ScheduledAt.Should().Be(sc.Ten.AddMinutes(60));
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShiftOtherPitches_MovesTheClashingMatchAndLaterOnesOnItsPitch_ButNotEarlierOnes()
        {
            var sc = OtherPitchScenario();

            var result = await _handler.Handle(
                new UpdateMatchScheduleCommand(sc.TournamentId, sc.Match.Id, sc.Pitch, sc.Ten.AddMinutes(10), ShiftFollowing: true, ShiftOtherPitches: true),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            sc.Next.ScheduledAt.Should().Be(sc.Ten.AddMinutes(40));
            sc.Elsewhere.ScheduledAt.Should().Be(sc.Ten.AddMinutes(70));
            sc.ElsewhereNext.ScheduledAt.Should().Be(sc.Ten.AddMinutes(100));
            sc.ElsewhereEarlier.ScheduledAt.Should().Be(sc.Ten);
        }

        [Fact]
        public async Task Handle_ShiftFollowing_DoesNotTouchOtherPitches_WhenNothingClashes()
        {
            var sc = OtherPitchScenario();

            // Mecz tej samej drużyny na B z zapasem czasu: po opóźnieniu o 10 min dalej nie koliduje.
            sc.Elsewhere.UpdateSchedule(sc.Elsewhere.PitchId, sc.Ten.AddMinutes(120), Guid.NewGuid());
            sc.ElsewhereNext.UpdateSchedule(sc.ElsewhereNext.PitchId, sc.Ten.AddMinutes(150), Guid.NewGuid());

            var result = await _handler.Handle(
                new UpdateMatchScheduleCommand(sc.TournamentId, sc.Match.Id, sc.Pitch, sc.Ten.AddMinutes(10), ShiftFollowing: true), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            sc.Next.ScheduledAt.Should().Be(sc.Ten.AddMinutes(40));
            sc.Elsewhere.ScheduledAt.Should().Be(sc.Ten.AddMinutes(120));
            sc.ElsewhereNext.ScheduledAt.Should().Be(sc.Ten.AddMinutes(150));
        }

        [Fact]
        public async Task Handle_ShiftOtherPitches_IsRejected_WhenTheClashingMatchAlreadyStarted()
        {
            var sc = OtherPitchScenario();
            sc.Elsewhere.Start(Guid.NewGuid());

            var result = await _handler.Handle(
                new UpdateMatchScheduleCommand(sc.TournamentId, sc.Match.Id, sc.Pitch, sc.Ten.AddMinutes(10), ShiftFollowing: true, ShiftOtherPitches: true),
                CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("One of the teams already plays another match at this time");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        private sealed record OtherPitch(
            Guid TournamentId, Guid Pitch, DateTimeOffset Ten,
            Match Match, Match Next, Match ElsewhereEarlier, Match Elsewhere, Match ElsewhereNext);

        // Mecz trwa 30 min. Boisko A: Match 10:00, Next 10:30. Boisko B: ElsewhereEarlier 10:00,
        // Elsewhere 11:00 (drużyna z Next, zaraz po jej meczu), ElsewhereNext 11:30.
        private OtherPitch OtherPitchScenario()
        {
            var tournamentId = Guid.NewGuid();
            var pitch = Guid.NewGuid();
            var pitchB = Guid.NewGuid();
            var ten = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(1).AddHours(10), TimeSpan.Zero);
            var match = OtherMatch(tournamentId, pitch, ten);
            var next = OtherMatch(tournamentId, pitch, ten.AddMinutes(30));
            var elsewhereEarlier = OtherMatch(tournamentId, pitchB, ten);
            var elsewhere = Match.Create(Guid.NewGuid(), tournamentId, null, null, next.HomeTournamentTeamId, Guid.NewGuid(),
                pitchB, ten.AddMinutes(60), StageType.Group).Value;
            var elsewhereNext = OtherMatch(tournamentId, pitchB, ten.AddMinutes(90));
            ShiftScenario(tournamentId, match, next, elsewhereEarlier, elsewhere, elsewhereNext);
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 0, 60, 3, 1, 0, false, false, 3, 0));
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            return new OtherPitch(tournamentId, pitch, ten, match, next, elsewhereEarlier, elsewhere, elsewhereNext);
        }
    }
}
