using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class AdvanceBracketWinnerCommandHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly IStandingModule _standingModule;
        private readonly AdvanceBracketWinnerCommandHandler _handler;

        public AdvanceBracketWinnerCommandHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _standingModule = Substitute.For<IStandingModule>();
            var advancer = new BracketAdvancer(_dbContext, _matchModule, _standingModule);
            _handler = new AdvanceBracketWinnerCommandHandler(_dbContext, advancer);
        }

        private static BracketNode CreateResolvedNode(Guid homeTeamId, Guid awayTeamId, Guid bracketRoundId)
        {
            var node = BracketNode.Create(
                Guid.NewGuid(), bracketRoundId,
                BracketSourceType.Fixed, null, null, null, homeTeamId,
                BracketSourceType.Fixed, null, null, null, awayTeamId).Value;
            node.AssignMatch(Guid.NewGuid());
            return node;
        }

        [Fact]
        public async Task Handle_ShouldResolveWinnerSlot_AndCreateMatch_WhenTargetBecomesFullyResolved()
        {
            // Arrange
            var bracketId = Guid.NewGuid();
            var roundId = Guid.NewGuid();
            var bracket = Bracket.Create(bracketId, Guid.NewGuid(), Guid.NewGuid()).Value;
            var winnerId = Guid.NewGuid();
            var loserId = Guid.NewGuid();
            var sourceNode = CreateResolvedNode(winnerId, loserId, roundId);

            var targetRoundId = Guid.NewGuid();
            var targetRound = Turniejo.Brackets.Domain.Entities.BracketRound.Create(Guid.NewGuid(), bracketId, 2, BracketRoundType.Standard, "Final").Value;
            var otherTeamId = Guid.NewGuid();
            var targetNode = BracketNode.Create(
                Guid.NewGuid(), targetRoundId,
                BracketSourceType.MatchWinner, null, null, sourceNode.Id, null,
                BracketSourceType.Fixed, null, null, null, otherTeamId).Value;

            sourceNode.WireAdvancement(targetNode.Id, BracketSlot.Home, null, null);

            _dbContext.GetBracketNodeByMatchIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(sourceNode);
            _dbContext.GetBracketRoundByIdAsync(roundId, Arg.Any<CancellationToken>()).Returns(
                Turniejo.Brackets.Domain.Entities.BracketRound.Create(Guid.NewGuid(), bracketId, 1, BracketRoundType.Standard, "Round 1").Value);
            _dbContext.GetBracketByIdAsync(bracketId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketNodeByIdAsync(targetNode.Id, Arg.Any<CancellationToken>()).Returns(targetNode);
            _dbContext.GetBracketRoundByIdAsync(targetRoundId, Arg.Any<CancellationToken>()).Returns(targetRound);
            _matchModule.CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(Guid.NewGuid());

            var command = new AdvanceBracketWinnerCommand(Guid.NewGuid(), winnerId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Advanced.Should().BeTrue();
            targetNode.HomeTournamentTeamId.Should().Be(winnerId);
            await _matchModule.Received(1).CreateBracketMatchAsync(
                bracket.TournamentId, bracket.CategoryId, winnerId, otherTeamId, "Final", Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAdvanceLoser_ToThirdPlaceMatch_WhenLoserGoesToNodeIdIsWired()
        {
            // Zamyka lukę z audytu testów: generowanie drabinki z meczem o 3. miejsce jest testowane,
            // ale nikt nie sprawdzał, że przegrany półfinalista faktycznie LĄDUJE w tym meczu po
            // rzeczywistym zakończeniu półfinału — tylko ścieżka zwycięzcy miała pokrycie.
            var bracketId = Guid.NewGuid();
            var roundId = Guid.NewGuid();
            var bracket = Bracket.Create(bracketId, Guid.NewGuid(), Guid.NewGuid()).Value;
            var winnerId = Guid.NewGuid();
            var loserId = Guid.NewGuid();
            var sourceNode = CreateResolvedNode(winnerId, loserId, roundId);

            var thirdPlaceRoundId = Guid.NewGuid();
            var thirdPlaceRound = BracketRound.Create(Guid.NewGuid(), bracketId, 2, BracketRoundType.ThirdPlace, "3. miejsce").Value;
            var otherLoserId = Guid.NewGuid();
            var thirdPlaceNode = BracketNode.Create(
                Guid.NewGuid(), thirdPlaceRoundId,
                BracketSourceType.MatchLoser, null, null, sourceNode.Id, null,
                BracketSourceType.Fixed, null, null, null, otherLoserId).Value;

            // Tylko ścieżka przegranego jest wpięta — WinnerGoesToNodeId zostaje null, żeby test
            // był izolowany dokładnie na LoserGoesToNodeId.
            sourceNode.WireAdvancement(null, null, thirdPlaceNode.Id, BracketSlot.Home);

            _dbContext.GetBracketNodeByMatchIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(sourceNode);
            _dbContext.GetBracketRoundByIdAsync(roundId, Arg.Any<CancellationToken>()).Returns(
                BracketRound.Create(Guid.NewGuid(), bracketId, 1, BracketRoundType.Standard, "Półfinał").Value);
            _dbContext.GetBracketByIdAsync(bracketId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketNodeByIdAsync(thirdPlaceNode.Id, Arg.Any<CancellationToken>()).Returns(thirdPlaceNode);
            _dbContext.GetBracketRoundByIdAsync(thirdPlaceRoundId, Arg.Any<CancellationToken>()).Returns(thirdPlaceRound);
            _matchModule.CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(Guid.NewGuid());

            var command = new AdvanceBracketWinnerCommand(Guid.NewGuid(), winnerId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Advanced.Should().BeTrue();
            thirdPlaceNode.HomeTournamentTeamId.Should().Be(loserId);
            await _matchModule.Received(1).CreateBracketMatchAsync(
                bracket.TournamentId, bracket.CategoryId, loserId, otherLoserId, "ThirdPlace", Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnNotAdvanced_WhenMatchIsNotABracketMatch()
        {
            // Arrange
            _dbContext.GetBracketNodeByMatchIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((BracketNode?)null);
            var command = new AdvanceBracketWinnerCommand(Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Advanced.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_ShouldReturnNotAdvanced_WhenNoWinnerProvided()
        {
            // Arrange
            var sourceNode = CreateResolvedNode(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            _dbContext.GetBracketNodeByMatchIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(sourceNode);
            var command = new AdvanceBracketWinnerCommand(Guid.NewGuid(), null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Advanced.Should().BeFalse();
        }
    }
}
