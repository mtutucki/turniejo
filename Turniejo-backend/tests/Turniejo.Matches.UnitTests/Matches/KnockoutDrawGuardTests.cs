using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AddGoal;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Matches.RevertMatchEvent;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    // QA v3: PUC-049 - cofnięcie gola w oknie poprawek zostawiało zakończony mecz pucharowy z remisem bez karnych,
    // a w kolejnej rundzie dalej stał dotychczasowy zwycięzca. Decyzja: blokada, z prośbą o wpisanie karnych.
    public sealed class KnockoutDrawGuardTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ISender _sender = Substitute.For<ISender>();
        private readonly ICurrentUserContext _user = Substitute.For<ICurrentUserContext>();
        private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();

        public KnockoutDrawGuardTests()
        {
            _user.UserId.Returns(Guid.NewGuid());
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(0, 0)));
            _dbContext.GetActiveMatchEventsByTypeAsync(default, default, default).ReturnsForAnyArgs(new List<MatchEvent>());
        }

        // Mecz pucharowy zakończony 2:1 w czasie gry, okno poprawek otwarte.
        private (Match Match, List<MatchEvent> Goals) FinishedKnockout(StageType stage = StageType.Knockout)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, stage).Value;
            match.Start(Guid.NewGuid());
            var goals = new List<MatchEvent>
            {
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 5, Guid.NewGuid(), match.HomeTournamentTeamId).Value,
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 9, Guid.NewGuid(), match.HomeTournamentTeamId).Value,
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 12, Guid.NewGuid(), match.AwayTournamentTeamId).Value,
            };
            match.UpdateScore(2, 1);
            match.Finish(MatchResultType.Regular, null, 60, Guid.NewGuid()).IsSuccess.Should().BeTrue();
            match.ClearDomainEvents();

            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(goals);
            foreach (var goal in goals)
                _dbContext.GetMatchEventByIdAsync(goal.Id, Arg.Any<CancellationToken>()).Returns(goal);
            return (match, goals);
        }

        [Fact]
        public async Task PUC_049_RevertingWinnersGoal_InFinishedKnockout_IsRejected()
        {
            var (match, goals) = FinishedKnockout();
            var handler = new RevertMatchEventCommandHandler(_dbContext, _sender, _user);

            var result = await handler.Handle(new RevertMatchEventCommand(match.TournamentId, match.Id, goals[0].Id), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("A knockout match cannot end in a draw — decide it by penalties");
            goals[0].IsDeleted.Should().BeFalse();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task PUC_049_AddingEqualizer_InFinishedKnockout_IsRejected()
        {
            var (match, _) = FinishedKnockout();
            var handler = new AddGoalCommandHandler(_dbContext, new MatchEventRosterGuard(_playerModule), new SendOffRules(_dbContext), _sender, _user);

            var result = await handler.Handle(
                new AddGoalCommand(match.TournamentId, match.Id, match.AwayTournamentTeamId, null, null, MatchEventType.Goal, 20, null),
                CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            await _dbContext.DidNotReceive().AddMatchEventAsync(Arg.Any<MatchEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RevertingGoal_InFinishedGroupMatch_CanEndInDraw()
        {
            var (match, goals) = FinishedKnockout(StageType.Group);
            var handler = new RevertMatchEventCommandHandler(_dbContext, _sender, _user);

            var result = await handler.Handle(new RevertMatchEventCommand(match.TournamentId, match.Id, goals[0].Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task RevertingGoal_InKnockout_ThatKeepsAWinner_IsAllowed()
        {
            var (match, goals) = FinishedKnockout();
            var handler = new RevertMatchEventCommandHandler(_dbContext, _sender, _user);

            var result = await handler.Handle(new RevertMatchEventCommand(match.TournamentId, match.Id, goals[2].Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }
    }
}
