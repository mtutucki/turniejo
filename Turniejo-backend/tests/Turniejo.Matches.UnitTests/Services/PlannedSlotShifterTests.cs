using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Services
{
    public sealed class PlannedSlotShifterTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly IBracketModule _bracketModule = Substitute.For<IBracketModule>();
        private readonly PlannedSlotShifter _shifter;
        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _pitchId = Guid.NewGuid();
        private readonly DateTimeOffset _start = new(2026, 10, 1, 20, 0, 0, TimeSpan.Zero);

        public PlannedSlotShifterTests()
        {
            // Mecz 30 min + 10 min przerwy.
            _tournamentModule.GetTournamentSettingsAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(_tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0));
            _shifter = new PlannedSlotShifter(_dbContext, _tournamentModule, _bracketModule);
        }

        private Match SemiFinal(DateTimeOffset scheduledAt, Guid? pitchId) =>
            Match.Create(Guid.NewGuid(), _tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, scheduledAt, StageType.Knockout).Value;

        private void Arrange(List<Match> matches, params BracketNodeInfoDto[] nodes)
        {
            _dbContext.GetMatchesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(matches);
            _bracketModule.GetNodesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(nodes);
        }

        private async Task<List<BracketPlannedSlotDto>> ShiftAsync()
        {
            List<BracketPlannedSlotDto> shifted = [];
            await _bracketModule.SetPlannedSlotsAsync(
                Arg.Do<IReadOnlyCollection<BracketPlannedSlotDto>>(s => shifted = s.ToList()), Arg.Any<CancellationToken>());

            await _shifter.ShiftAsync(_tournamentId, CancellationToken.None);

            return shifted;
        }

        [Fact]
        public async Task ShiftAsync_ShouldMoveFinalAfterRescheduledSemiFinal()
        {
            // Arrange — półfinał 2 przesunięty ręcznie tuż przed planowany finał, jedno boisko.
            var semi1 = SemiFinal(_start, _pitchId);
            var semi2 = SemiFinal(_start.AddMinutes(75), _pitchId);
            var semi1Node = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, semi1.Id, null, null, false);
            var semi2Node = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, semi2.Id, null, null, false);
            var finalNode = new BracketNodeInfoDto(Guid.NewGuid(), 2, false, null, semi1Node.Id, semi2Node.Id, false, _pitchId, _start.AddMinutes(80));
            Arrange([semi1, semi2], semi1Node, semi2Node, finalNode);

            // Act
            var shifted = await ShiftAsync();

            // Assert — finał po półfinale 2 i przerwie.
            shifted.Should().ContainSingle()
                .Which.Should().Be(new BracketPlannedSlotDto(finalNode.Id, _pitchId, _start.AddMinutes(115)));
        }

        [Fact]
        public async Task ShiftAsync_ShouldCascadeThroughPlannedRounds()
        {
            // Arrange — mecz o 3. miejsce i finał zaplanowane jeden po drugim na tym samym boisku.
            var semi1 = SemiFinal(_start, _pitchId);
            var semi2 = SemiFinal(_start.AddMinutes(60), _pitchId);
            var semi1Node = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, semi1.Id, null, null, false);
            var semi2Node = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, semi2.Id, null, null, false);
            var thirdNode = new BracketNodeInfoDto(Guid.NewGuid(), 2, true, null, semi1Node.Id, semi2Node.Id, false, _pitchId, _start.AddMinutes(80));
            var finalNode = new BracketNodeInfoDto(Guid.NewGuid(), 2, false, null, semi1Node.Id, semi2Node.Id, false, _pitchId, _start.AddMinutes(120));
            Arrange([semi1, semi2], semi1Node, semi2Node, finalNode, thirdNode);

            // Act
            var shifted = await ShiftAsync();

            // Assert
            shifted.Should().BeEquivalentTo(
            [
                new BracketPlannedSlotDto(thirdNode.Id, _pitchId, _start.AddMinutes(100)),
                new BracketPlannedSlotDto(finalNode.Id, _pitchId, _start.AddMinutes(140)),
            ]);
        }

        [Fact]
        public async Task ShiftAsync_ShouldLeavePlannedSlot_WhenNothingCollides()
        {
            // Arrange
            var semi1 = SemiFinal(_start, _pitchId);
            var semi2 = SemiFinal(_start.AddMinutes(40), _pitchId);
            var semi1Node = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, semi1.Id, null, null, false);
            var semi2Node = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, semi2.Id, null, null, false);
            var finalNode = new BracketNodeInfoDto(Guid.NewGuid(), 2, false, null, semi1Node.Id, semi2Node.Id, false, _pitchId, _start.AddMinutes(80));
            Arrange([semi1, semi2], semi1Node, semi2Node, finalNode);

            // Act
            var shifted = await ShiftAsync();

            // Assert
            shifted.Should().BeEmpty();
        }
    }
}
