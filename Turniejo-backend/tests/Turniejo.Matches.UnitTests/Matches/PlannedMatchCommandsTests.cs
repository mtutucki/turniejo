using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AssignPlannedMatchReferee;
using Turniejo.Matches.Application.Matches.UpdatePlannedMatchSchedule;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    // Mecz pucharu, który jeszcze nie powstał (finał czekający na półfinały): termin i sędzia z góry.
    public sealed class PlannedMatchCommandsTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly IBracketModule _bracketModule = Substitute.For<IBracketModule>();
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly DateTimeOffset _start = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);
        private readonly Pitch _pitch;
        private readonly Match _semi;
        private readonly BracketNodeInfoDto _semiNode;
        private readonly BracketNodeInfoDto _finalNode;

        public PlannedMatchCommandsTests()
        {
            _pitch = Pitch.Create(Guid.NewGuid(), _tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            _semi = Match.Create(Guid.NewGuid(), _tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), _pitch.Id, _start, StageType.Knockout).Value;
            _semiNode = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, _semi.Id, null, null, false);
            _finalNode = new BracketNodeInfoDto(Guid.NewGuid(), 2, false, null, _semiNode.Id, null, false, _pitch.Id, _start.AddHours(2));

            _bracketModule.GetNodesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<BracketNodeInfoDto> { _semiNode, _finalNode });
            _dbContext.GetMatchesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { _semi });
            _dbContext.GetPitchByIdAsync(_pitch.Id, Arg.Any<CancellationToken>()).Returns(_pitch);
            _tournamentModule.GetTournamentAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(_tournamentId, "Puchar", "ScheduleGenerated", "Knockout", Guid.NewGuid(), 8, _start.AddHours(-1), _start.AddHours(8)));
            // 30 min gry + 5 min przerwy.
            _tournamentModule.GetTournamentSettingsAsync(default, default).ReturnsForAnyArgs(
                ci => new TournamentSettingsSummaryDto(ci.Arg<Guid>(), 30, 5, 60, 3, 1, 0, false, false, 3, 0));
        }

        private UpdatePlannedMatchScheduleCommandHandler ScheduleHandler()
        {
            var context = new PlannedMatchContext(_dbContext, _tournamentModule, _bracketModule);
            var shifter = new PlannedSlotShifter(_dbContext, _tournamentModule, _bracketModule);
            return new UpdatePlannedMatchScheduleCommandHandler(_dbContext, context, shifter, _bracketModule);
        }

        private AssignPlannedMatchRefereeCommandHandler RefereeHandler() =>
            new(new PlannedMatchContext(_dbContext, _tournamentModule, _bracketModule), _authorizationService, _bracketModule);

        [Fact]
        public async Task Schedule_ShouldSavePlannedSlot_WhenFree()
        {
            var at = _start.AddHours(1);

            var result = await ScheduleHandler().Handle(
                new UpdatePlannedMatchScheduleCommand(_tournamentId, _finalNode.Id, _pitch.Id, at), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _bracketModule.Received().SetPlannedSlotsAsync(
                Arg.Is<IReadOnlyCollection<BracketPlannedSlotDto>>(s => s.Single().NodeId == _finalNode.Id && s.Single().ScheduledAt == at),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Schedule_ShouldFail_WhenBeforeTheSourceMatchEnds()
        {
            // Półfinał 9:00 + 35 min: finał o 9:20 nie ma jeszcze drużyny.
            var result = await ScheduleHandler().Handle(
                new UpdatePlannedMatchScheduleCommand(_tournamentId, _finalNode.Id, _pitch.Id, _start.AddMinutes(20)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.ScheduledAt");
        }

        [Fact]
        public async Task Schedule_ShouldFail_WhenNodeAlreadyHasAMatch()
        {
            var result = await ScheduleHandler().Handle(
                new UpdatePlannedMatchScheduleCommand(_tournamentId, _semiNode.Id, _pitch.Id, _start.AddHours(1)), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            await _bracketModule.DidNotReceiveWithAnyArgs().SetPlannedSlotsAsync(default!, default);
        }

        [Fact]
        public async Task Referee_ShouldBeSaved_WhenTournamentReferee()
        {
            var refereeId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(default, default!, default, default!, default).ReturnsForAnyArgs(true);

            var result = await RefereeHandler().Handle(
                new AssignPlannedMatchRefereeCommand(_tournamentId, _finalNode.Id, refereeId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _bracketModule.Received(1).SetPlannedRefereeAsync(_finalNode.Id, refereeId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Referee_ShouldFail_WhenNotATournamentReferee()
        {
            _authorizationService.HasAnyRoleAsync(default, default!, default, default!, default).ReturnsForAnyArgs(false);

            var result = await RefereeHandler().Handle(
                new AssignPlannedMatchRefereeCommand(_tournamentId, _finalNode.Id, Guid.NewGuid()), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RefereeId");
            await _bracketModule.DidNotReceiveWithAnyArgs().SetPlannedRefereeAsync(default, default, default);
        }

        [Fact]
        public async Task Referee_ShouldFail_WhenRefereeHasAnotherMatchThen()
        {
            var refereeId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(default, default!, default, default!, default).ReturnsForAnyArgs(true);
            var busy = Match.Create(Guid.NewGuid(), _tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, _start.AddHours(2).AddMinutes(10), StageType.Knockout).Value;
            busy.AssignReferee(refereeId, Guid.NewGuid());
            _dbContext.GetMatchesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { _semi, busy });

            var result = await RefereeHandler().Handle(
                new AssignPlannedMatchRefereeCommand(_tournamentId, _finalNode.Id, refereeId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.RefereeId");
        }
    }
}
