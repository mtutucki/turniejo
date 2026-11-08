using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Matches.SetMatchScore;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    // QA v3: MNZ-018 - ręczny wynik przepadał przy kolejnym golu, bo wynik liczy się od nowa ze zdarzeń.
    // Decyzja: ręczna zmiana dopisuje brakujące gole bez strzelca, a przy obniżeniu cofa najpierw gole bez strzelca.
    public sealed class SetMatchScoreCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
        private readonly ISender _sender = Substitute.For<ISender>();
        private readonly List<MatchEvent> _added = [];
        private readonly SetMatchScoreCommandHandler _handler;

        public SetMatchScoreCommandHandlerTests()
        {
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            _dbContext.AddMatchEventAsync(Arg.Do<MatchEvent>(e => _added.Add(e)), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(call => Result.Success(new RecalculateMatchScoreResponse(0, 0)));
            _handler = new SetMatchScoreCommandHandler(_dbContext, new GoalAdjuster(_dbContext), _sender, _currentUserContext);
        }

        private Match LiveMatch(params MatchEvent[] goals)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(goals.ToList());
            foreach (var goal in goals)
                _dbContext.GetMatchEventByIdAsync(goal.Id, Arg.Any<CancellationToken>()).Returns(goal);
            return match;
        }

        private static MatchEvent Goal(Guid matchId, Guid teamId, Guid? playerId = null, int minute = 5) =>
            MatchEvent.Create(Guid.NewGuid(), matchId, MatchEventType.Goal, minute, Guid.NewGuid(), teamId, playerId).Value;

        [Fact]
        public async Task MNZ_018_RaisingTheScore_AddsGoalsWithoutScorer_AndKeepsTheResultRegular()
        {
            var matchId = Guid.NewGuid();
            var match = LiveMatch();
            var homeGoal = Goal(match.Id, match.HomeTournamentTeamId, Guid.NewGuid());
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([homeGoal]);

            var result = await _handler.Handle(new SetMatchScoreCommand(match.TournamentId, match.Id, 3, 2), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            _added.Count(e => e.TournamentTeamId == match.HomeTournamentTeamId).Should().Be(2);
            _added.Count(e => e.TournamentTeamId == match.AwayTournamentTeamId).Should().Be(2);
            _added.Should().OnlyContain(e => e.Type == MatchEventType.Goal && e.PlayerId == null);
            match.ResultType.Should().NotBe(MatchResultType.ManualDecision);
            await _sender.Received(1).Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.EntityType == "Match" && a.Action == "SetManualScore"), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MNZ_018_LoweringTheScore_RevertsGoalsWithoutScorerFirst()
        {
            var match = LiveMatch();
            var withScorer = Goal(match.Id, match.HomeTournamentTeamId, Guid.NewGuid(), minute: 20);
            var withoutScorer = Goal(match.Id, match.HomeTournamentTeamId, null, minute: 5);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns([withScorer, withoutScorer]);
            _dbContext.GetMatchEventByIdAsync(withScorer.Id, Arg.Any<CancellationToken>()).Returns(withScorer);
            _dbContext.GetMatchEventByIdAsync(withoutScorer.Id, Arg.Any<CancellationToken>()).Returns(withoutScorer);

            var result = await _handler.Handle(new SetMatchScoreCommand(match.TournamentId, match.Id, 1, 0), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            withoutScorer.IsDeleted.Should().BeTrue();
            withScorer.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchIsScheduled()
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new SetMatchScoreCommand(match.TournamentId, match.Id, 1, 0), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            var result = await _handler.Handle(new SetMatchScoreCommand(Guid.NewGuid(), matchId, 1, 0), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchIsFinished()
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            match.Finish(MatchResultType.Regular, null, 60, Guid.NewGuid());
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);

            var result = await _handler.Handle(new SetMatchScoreCommand(match.TournamentId, match.Id, 1, 0), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
        }
    }
}
