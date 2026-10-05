using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.SwapBracketSlots;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class SwapBracketSlotsCommandHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly SwapBracketSlotsCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();
        private readonly Bracket _bracket;
        private readonly BracketRound _roundOne;
        private readonly BracketRound _final;
        private readonly List<BracketNode> _nodes = [];

        public SwapBracketSlotsCommandHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _handler = new SwapBracketSlotsCommandHandler(
                _dbContext, _matchModule, new BracketAdvancer(_dbContext, _matchModule, Substitute.For<IStandingModule>()));

            _bracket = Bracket.Create(Guid.NewGuid(), _tournamentId, _categoryId).Value;
            _roundOne = BracketRound.Create(Guid.NewGuid(), _bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            _final = BracketRound.Create(Guid.NewGuid(), _bracket.Id, 2, BracketRoundType.Standard, "Final").Value;

            _dbContext.GetBracketByTournamentAndCategoryAsync(_tournamentId, _categoryId, Arg.Any<CancellationToken>()).Returns(_bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(_bracket.Id, Arg.Any<CancellationToken>()).Returns([_roundOne, _final]);
            _dbContext.GetBracketRoundByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => new[] { _roundOne, _final }.FirstOrDefault(r => r.Id == call.Arg<Guid>()));
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(_ => _nodes);
            _dbContext.GetTrackedBracketNodesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(call => _nodes.Where(n => call.Arg<IReadOnlyCollection<Guid>>().Contains(n.Id)).ToList());
            _matchModule.ChangeUnstartedMatchTeamsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
            _matchModule.RemoveUnstartedMatchesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        // Półfinały (1. runda) + finał. null = wolny los.
        private (BracketNode SemiA, BracketNode SemiB, BracketNode Final) SetupBracket(
            Guid? aHome, Guid? aAway, Guid? bHome, Guid? bAway)
        {
            BracketNode Semi(Guid? home, Guid? away) => BracketNode.Create(
                Guid.NewGuid(), _roundOne.Id,
                home.HasValue ? BracketSourceType.Fixed : BracketSourceType.Bye, null, null, null, home,
                away.HasValue ? BracketSourceType.Fixed : BracketSourceType.Bye, null, null, null, away).Value;

            var semiA = Semi(aHome, aAway);
            var semiB = Semi(bHome, bAway);
            var final = BracketNode.Create(
                Guid.NewGuid(), _final.Id,
                BracketSourceType.MatchWinner, null, null, semiA.Id, null,
                BracketSourceType.MatchWinner, null, null, semiB.Id, null).Value;
            semiA.WireAdvancement(final.Id, BracketSlot.Home, null, null);
            semiB.WireAdvancement(final.Id, BracketSlot.Away, null, null);

            _nodes.AddRange([semiA, semiB, final]);
            return (semiA, semiB, final);
        }

        private MatchInfoDto SetupMatch(BracketNode node, string status = "Scheduled")
        {
            var match = new MatchInfoDto(
                Guid.NewGuid(), _tournamentId, node.HomeTournamentTeamId!.Value, node.AwayTournamentTeamId!.Value,
                null, Guid.NewGuid(), new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), status);
            node.AssignMatch(match.Id);
            _matchModule.GetMatchAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            return match;
        }

        private SwapBracketSlotsCommand Swap(BracketNode first, BracketSlot firstSlot, BracketNode second, BracketSlot secondSlot) =>
            new(_tournamentId, _categoryId, first.Id, firstSlot, second.Id, secondSlot);

        [Fact]
        public async Task Handle_ShouldSwapTeams_AndUpdateBothMatches_KeepingTheMatches()
        {
            // Arrange
            var (a, b, c, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var (semiA, semiB, _) = SetupBracket(a, b, c, d);
            var matchA = SetupMatch(semiA);
            var matchB = SetupMatch(semiB);

            // Act
            var result = await _handler.Handle(Swap(semiA, BracketSlot.Away, semiB, BracketSlot.Home), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            semiA.AwayTournamentTeamId.Should().Be(c);
            semiB.HomeTournamentTeamId.Should().Be(b);
            semiA.MatchId.Should().Be(matchA.Id);
            await _matchModule.Received(1).ChangeUnstartedMatchTeamsAsync(matchA.Id, a, c, Arg.Any<CancellationToken>());
            await _matchModule.Received(1).ChangeUnstartedMatchTeamsAsync(matchB.Id, b, d, Arg.Any<CancellationToken>());
            await _matchModule.DidNotReceive().RemoveUnstartedMatchesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldMoveByeToOtherPair_RemovingItsMatch_AndReusingTheSlotForTheNewPair()
        {
            // Arrange: A ma wolny los (jest już w finale), B i C grają mecz.
            var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var (semiA, semiB, final) = SetupBracket(a, null, b, c);
            final.ResolveSlot(BracketSlot.Home, a);
            var matchB = SetupMatch(semiB);
            var newMatchId = Guid.NewGuid();
            _matchModule.CreateBracketMatchAsync(
                    _tournamentId, _categoryId, a, c, "Knockout", matchB.PitchId, matchB.ScheduledAt, Arg.Any<CancellationToken>())
                .Returns(newMatchId);

            // Act: wolny los przechodzi do pary B.
            var result = await _handler.Handle(Swap(semiA, BracketSlot.Away, semiB, BracketSlot.Away), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            semiA.AwayTournamentTeamId.Should().Be(c);
            semiA.MatchId.Should().Be(newMatchId);
            semiB.AwaySourceType.Should().Be(BracketSourceType.Bye);
            semiB.MatchId.Should().BeNull();
            semiB.PlannedAt.Should().BeNull();
            final.HomeTournamentTeamId.Should().BeNull();
            final.AwayTournamentTeamId.Should().Be(b);
            await _matchModule.Received(1).RemoveUnstartedMatchesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == matchB.Id), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnConflict_AndChangeNothing_WhenABracketMatchHasStarted()
        {
            // Arrange
            var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
            var (semiA, semiB, _) = SetupBracket(a, b, Guid.NewGuid(), Guid.NewGuid());
            SetupMatch(semiA);
            SetupMatch(semiB, "Live");

            // Act
            var result = await _handler.Handle(Swap(semiA, BracketSlot.Home, semiB, BracketSlot.Home), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Cannot change the pairings once a bracket match has started, finished or been cancelled");
            semiA.HomeTournamentTeamId.Should().Be(a);
            await _matchModule.DidNotReceive().ChangeUnstartedMatchTeamsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnValidation_WhenSlotIsNotInFirstRound()
        {
            // Arrange
            var (semiA, _, final) = SetupBracket(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await _handler.Handle(Swap(semiA, BracketSlot.Home, final, BracketSlot.Home), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Only first-round slots can be swapped");
        }

        [Fact]
        public async Task Handle_ShouldReturnValidation_WhenPairWouldHaveByeOnBothSides()
        {
            // Arrange
            var a = Guid.NewGuid();
            var (semiA, semiB, _) = SetupBracket(a, null, Guid.NewGuid(), null);

            // Act
            var result = await _handler.Handle(Swap(semiA, BracketSlot.Home, semiB, BracketSlot.Away), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Bye");
            semiA.HomeTournamentTeamId.Should().Be(a);
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenBracketDoesNotExist()
        {
            // Arrange
            _dbContext.GetBracketByTournamentAndCategoryAsync(_tournamentId, _categoryId, Arg.Any<CancellationToken>()).Returns((Bracket?)null);

            // Act
            var result = await _handler.Handle(
                new SwapBracketSlotsCommand(_tournamentId, _categoryId, Guid.NewGuid(), BracketSlot.Home, Guid.NewGuid(), BracketSlot.Away),
                CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
