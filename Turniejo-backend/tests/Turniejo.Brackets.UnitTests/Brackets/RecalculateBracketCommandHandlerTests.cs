using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.RecalculateBracket;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class RecalculateBracketCommandHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly IMatchModule _matchModule;
        private readonly RecalculateBracketCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();
        private readonly BracketNode _semiA;
        private readonly BracketNode _semiB;
        private readonly BracketNode _final;
        private readonly Guid _a = Guid.NewGuid(), _b = Guid.NewGuid(), _c = Guid.NewGuid(), _d = Guid.NewGuid();
        private readonly Guid _semiAMatch = Guid.NewGuid(), _semiBMatch = Guid.NewGuid(), _finalMatch = Guid.NewGuid();

        public RecalculateBracketCommandHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _matchModule = Substitute.For<IMatchModule>();
            _handler = new RecalculateBracketCommandHandler(
                _dbContext, _matchModule, new BracketAdvancer(_dbContext, _matchModule, Substitute.For<IStandingModule>()));

            var bracket = Bracket.Create(Guid.NewGuid(), _tournamentId, _categoryId).Value;
            var roundOne = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var finalRound = BracketRound.Create(Guid.NewGuid(), bracket.Id, 2, BracketRoundType.Standard, "Final").Value;

            BracketNode Semi(Guid home, Guid away) => BracketNode.Create(
                Guid.NewGuid(), roundOne.Id,
                BracketSourceType.Fixed, null, null, null, home,
                BracketSourceType.Fixed, null, null, null, away).Value;

            _semiA = Semi(_a, _b);
            _semiB = Semi(_c, _d);
            _final = BracketNode.Create(
                Guid.NewGuid(), finalRound.Id,
                BracketSourceType.MatchWinner, null, null, _semiA.Id, null,
                BracketSourceType.MatchWinner, null, null, _semiB.Id, null).Value;
            _semiA.WireAdvancement(_final.Id, BracketSlot.Home, null, null);
            _semiB.WireAdvancement(_final.Id, BracketSlot.Away, null, null);
            _semiA.AssignMatch(_semiAMatch);
            _semiB.AssignMatch(_semiBMatch);

            // Stan po pomyłce: w finale stoi A, choć półfinał wygrał B.
            _final.ResolveSlot(BracketSlot.Home, _a);
            _final.ResolveSlot(BracketSlot.Away, _c);
            _final.AssignMatch(_finalMatch);

            var nodes = new List<BracketNode> { _semiA, _semiB, _final };
            _dbContext.GetBracketByTournamentAndCategoryAsync(_tournamentId, _categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([finalRound, roundOne]);
            _dbContext.GetBracketRoundByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => new[] { roundOne, finalRound }.FirstOrDefault(r => r.Id == call.Arg<Guid>()));
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(nodes);
            _dbContext.GetBracketNodeByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => nodes.FirstOrDefault(n => n.Id == call.Arg<Guid>()));
            _matchModule.RefreshKnockoutWinnersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new Dictionary<Guid, Guid?> { [_semiAMatch] = _b, [_semiBMatch] = _c });
        }

        [Fact]
        public async Task Handle_ShouldPutTheRightWinnerIntoNextMatch_WhenItHasNotStarted()
        {
            _matchModule.ChangeUnstartedMatchTeamsAsync(_finalMatch, _b, _c, Arg.Any<CancellationToken>()).Returns(true);

            var result = await _handler.Handle(new RecalculateBracketCommand(_tournamentId, _categoryId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Changed.Should().BeTrue();
            result.Value.BlockedMatchIds.Should().BeEmpty();
            _final.HomeTournamentTeamId.Should().Be(_b);
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReportNextMatch_WhenItHasAlreadyStarted()
        {
            _matchModule.ChangeUnstartedMatchTeamsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

            var result = await _handler.Handle(new RecalculateBracketCommand(_tournamentId, _categoryId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.BlockedMatchIds.Should().Equal(_finalMatch);
            _final.HomeTournamentTeamId.Should().Be(_a);
        }
    }
}
