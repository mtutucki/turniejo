using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.ResolveGroupPositionSlots;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class ResolveGroupPositionSlotsCommandHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly IStandingModule _standingModule;
        private readonly ResolveGroupPositionSlotsCommandHandler _handler;

        public ResolveGroupPositionSlotsCommandHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _standingModule = Substitute.For<IStandingModule>();
            _matchModule.IsGroupStageFinishedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
            var advancer = new BracketAdvancer(_dbContext, _matchModule, _standingModule);
            _handler = new ResolveGroupPositionSlotsCommandHandler(_dbContext, advancer);
        }

        [Fact]
        public async Task Handle_ShouldReturnFalse_WhenNoBracketExistsForCategory()
        {
            // Faza grupowa może się zakończyć zanim ktokolwiek wygeneruje drabinkę — to nie błąd.
            _dbContext.GetBracketByTournamentAndCategoryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((Bracket?)null);

            var command = new ResolveGroupPositionSlotsCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ResolvedAny.Should().BeFalse();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldResolveSlotAndCreateMatch_WhenGroupPositionNodeBecomesFullyResolved()
        {
            // Domyka lukę z audytu: drabinka wygenerowana przed końcem fazy grupowej ma się sama
            // dopiąć, gdy tabela grupy się zaktualizuje (StandingUpdated).
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var winnerTeamId = Guid.NewGuid();
            var fixedOpponentId = Guid.NewGuid();

            var bracket = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value;
            var round = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var node = BracketNode.Create(
                Guid.NewGuid(), round.Id,
                BracketSourceType.GroupPosition, groupId, 1, null, null,
                BracketSourceType.Fixed, null, null, null, fixedOpponentId).Value;

            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);
            _dbContext.GetTrackedBracketNodesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);
            _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
                .Returns([new GroupStandingDto(winnerTeamId, 1, 12)]);
            _matchModule.CreateBracketMatchAsync(
                tournamentId, categoryId, winnerTeamId, fixedOpponentId, Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(Guid.NewGuid());

            var command = new ResolveGroupPositionSlotsCommand(tournamentId, categoryId, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ResolvedAny.Should().BeTrue();
            node.HomeTournamentTeamId.Should().Be(winnerTeamId);
            await _matchModule.Received(1).CreateBracketMatchAsync(
                tournamentId, categoryId, winnerTeamId, fixedOpponentId, Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotResolveSlot_WhileGroupStageIsStillInProgress()
        {
            // Tabela w trakcie fazy grupowej to stan przejściowy — slot obsadzony za wcześnie zostawał
            // z drużyną, która ostatecznie zajęła inne miejsce.
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();

            var bracket = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value;
            var round = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var node = BracketNode.Create(
                Guid.NewGuid(), round.Id,
                BracketSourceType.GroupPosition, groupId, 1, null, null,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;

            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);
            _dbContext.GetTrackedBracketNodesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);
            _matchModule.IsGroupStageFinishedAsync(groupId, Arg.Any<CancellationToken>()).Returns(false);
            _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
                .Returns([new GroupStandingDto(Guid.NewGuid(), 1, 3)]);

            var command = new ResolveGroupPositionSlotsCommand(tournamentId, categoryId, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Value.ResolvedAny.Should().BeFalse();
            node.HomeTournamentTeamId.Should().BeNull();
            await _matchModule.DidNotReceive().CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldIgnoreNodes_ReferencingADifferentGroup()
        {
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var targetGroupId = Guid.NewGuid();
            var otherGroupId = Guid.NewGuid();

            var bracket = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value;
            var round = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var node = BracketNode.Create(
                Guid.NewGuid(), round.Id,
                BracketSourceType.GroupPosition, otherGroupId, 1, null, null,
                BracketSourceType.GroupPosition, otherGroupId, 2, null, null).Value;

            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);
            _dbContext.GetTrackedBracketNodesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);

            var command = new ResolveGroupPositionSlotsCommand(tournamentId, categoryId, targetGroupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ResolvedAny.Should().BeFalse();
            node.HomeTournamentTeamId.Should().BeNull();
            await _standingModule.DidNotReceive().GetGroupStandingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAdvanceByeTeam_WhenGroupPositionSideOfAByeNodeResolves()
        {
            // Kombinacja z audytu: "1. miejsce grupy A dostaje bye" — druga strona nigdy nie istnieje,
            // więc IsFullyResolved nigdy nie będzie true, a mimo to drużyna musi awansować dalej.
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var advancingTeamId = Guid.NewGuid();

            var bracket = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value;
            var round1 = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var round2 = BracketRound.Create(Guid.NewGuid(), bracket.Id, 2, BracketRoundType.Standard, "Final").Value;

            var byeNode = BracketNode.Create(
                Guid.NewGuid(), round1.Id,
                BracketSourceType.GroupPosition, groupId, 1, null, null,
                BracketSourceType.Bye, null, null, null, null).Value;
            var finalNode = BracketNode.Create(
                Guid.NewGuid(), round2.Id,
                BracketSourceType.MatchWinner, null, null, Guid.NewGuid(), null,
                BracketSourceType.MatchWinner, null, null, byeNode.Id, null).Value;
            byeNode.WireAdvancement(finalNode.Id, BracketSlot.Away, null, null);

            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round1, round2]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([byeNode]);
            _dbContext.GetTrackedBracketNodesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([byeNode]);
            _dbContext.GetBracketNodeByIdAsync(finalNode.Id, Arg.Any<CancellationToken>()).Returns(finalNode);
            _dbContext.GetBracketRoundByIdAsync(round2.Id, Arg.Any<CancellationToken>()).Returns(round2);
            _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
                .Returns([new GroupStandingDto(advancingTeamId, 1, 15)]);

            var command = new ResolveGroupPositionSlotsCommand(tournamentId, categoryId, groupId);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.ResolvedAny.Should().BeTrue();
            byeNode.HomeTournamentTeamId.Should().Be(advancingTeamId);
            finalNode.AwayTournamentTeamId.Should().Be(advancingTeamId);
            await _matchModule.DidNotReceive().CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());
        }
    }
}
