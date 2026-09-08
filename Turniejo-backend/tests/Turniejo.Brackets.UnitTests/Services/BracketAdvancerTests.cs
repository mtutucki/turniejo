using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.UnitTests.Services
{
    public sealed class BracketAdvancerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly IStandingModule _standingModule;
        private readonly BracketAdvancer _advancer;

        public BracketAdvancerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _standingModule = Substitute.For<IStandingModule>();
            _advancer = new BracketAdvancer(_dbContext, _matchModule, _standingModule);
        }

        private static BracketNode CreateEmptyNode(Guid roundId) =>
            BracketNode.Create(
                Guid.NewGuid(), roundId,
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null,
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null).Value;

        [Fact]
        public async Task AdvanceIntoAsync_ShouldResolveSlotAndCreateMatch_WhenBothSidesBecomeResolved()
        {
            var round = BracketRound.Create(Guid.NewGuid(), Guid.NewGuid(), 2, BracketRoundType.Standard, "Final").Value;
            var node = CreateEmptyNode(round.Id);
            var teamId = Guid.NewGuid();
            var otherTeamId = Guid.NewGuid();
            node.ResolveSlot(BracketSlot.Away, otherTeamId);
            _dbContext.GetBracketNodeByIdAsync(node.Id, Arg.Any<CancellationToken>()).Returns(node);
            _dbContext.GetBracketRoundByIdAsync(round.Id, Arg.Any<CancellationToken>()).Returns(round);
            _matchModule.CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Guid.NewGuid());

            var result = await _advancer.AdvanceIntoAsync(node.Id, BracketSlot.Home, teamId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            result.Should().BeTrue();
            node.HomeTournamentTeamId.Should().Be(teamId);
            node.MatchId.Should().NotBeNull();
        }

        [Fact]
        public async Task AdvanceIntoAsync_ShouldOverwriteSlot_WhenTargetNodeHasNoMatchYet_AndTeamChanges()
        {
            // Korekta wyniku poprzedniego meczu zmieniła zwycięzcę PRZED tym, jak kolejny mecz powstał —
            // to jest bezpieczny, dozwolony przypadek: nic jeszcze nie zostało "zamrożone".
            var round = BracketRound.Create(Guid.NewGuid(), Guid.NewGuid(), 2, BracketRoundType.Standard, "Final").Value;
            var node = CreateEmptyNode(round.Id);
            var oldTeamId = Guid.NewGuid();
            var newTeamId = Guid.NewGuid();
            node.ResolveSlot(BracketSlot.Home, oldTeamId);
            _dbContext.GetBracketNodeByIdAsync(node.Id, Arg.Any<CancellationToken>()).Returns(node);

            var result = await _advancer.AdvanceIntoAsync(node.Id, BracketSlot.Home, newTeamId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            result.Should().BeTrue();
            node.HomeTournamentTeamId.Should().Be(newTeamId);
        }

        [Fact]
        public async Task AdvanceIntoAsync_ShouldRefuseToOverwriteSlot_WhenTargetNodeAlreadyHasAMatch_AndTeamDiffers()
        {
            // Zamyka lukę z audytu: Match.HomeTournamentTeamId/AwayTournamentTeamId są niemutowalne po
            // utworzeniu, więc ciche nadpisanie samego węzła zostawiłoby węzeł i już istniejący mecz w
            // sprzecznych stanach — musi wymagać ręcznej interwencji, nie automatycznej podmianki.
            var round = BracketRound.Create(Guid.NewGuid(), Guid.NewGuid(), 2, BracketRoundType.Standard, "Final").Value;
            var node = CreateEmptyNode(round.Id);
            var oldTeamId = Guid.NewGuid();
            var newTeamId = Guid.NewGuid();
            node.ResolveSlot(BracketSlot.Home, oldTeamId);
            node.ResolveSlot(BracketSlot.Away, Guid.NewGuid());
            node.AssignMatch(Guid.NewGuid());
            _dbContext.GetBracketNodeByIdAsync(node.Id, Arg.Any<CancellationToken>()).Returns(node);

            var result = await _advancer.AdvanceIntoAsync(node.Id, BracketSlot.Home, newTeamId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            result.Should().BeFalse();
            node.HomeTournamentTeamId.Should().Be(oldTeamId);
            await _matchModule.DidNotReceive().CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task AdvanceIntoAsync_ShouldReturnTrue_WhenTargetNodeAlreadyHasAMatch_AndTeamIsUnchanged()
        {
            // Ponowne przetworzenie tego samego zwycięzcy (np. korekta wyniku, która NIE zmieniła
            // rezultatu) musi zostać no-opem, nie odmową.
            var round = BracketRound.Create(Guid.NewGuid(), Guid.NewGuid(), 2, BracketRoundType.Standard, "Final").Value;
            var node = CreateEmptyNode(round.Id);
            var teamId = Guid.NewGuid();
            node.ResolveSlot(BracketSlot.Home, teamId);
            node.ResolveSlot(BracketSlot.Away, Guid.NewGuid());
            node.AssignMatch(Guid.NewGuid());
            _dbContext.GetBracketNodeByIdAsync(node.Id, Arg.Any<CancellationToken>()).Returns(node);

            var result = await _advancer.AdvanceIntoAsync(node.Id, BracketSlot.Home, teamId, Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            result.Should().BeTrue();
            node.HomeTournamentTeamId.Should().Be(teamId);
        }

        [Fact]
        public async Task AdvanceIntoAsync_ShouldReturnFalse_WhenTargetNodeDoesNotExist()
        {
            _dbContext.GetBracketNodeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BracketNode?)null);

            var result = await _advancer.AdvanceIntoAsync(Guid.NewGuid(), BracketSlot.Home, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

            result.Should().BeFalse();
        }
    }
}
