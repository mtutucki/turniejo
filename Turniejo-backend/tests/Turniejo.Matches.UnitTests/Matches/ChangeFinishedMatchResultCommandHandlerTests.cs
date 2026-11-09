using Turniejo.Matches.Application.Services;
using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.ChangeFinishedMatchResult;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class ChangeFinishedMatchResultCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
        private readonly ChangeFinishedMatchResultCommandHandler _handler;

        public ChangeFinishedMatchResultCommandHandlerTests()
        {
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            _handler = new ChangeFinishedMatchResultCommandHandler(_dbContext, new GoalAdjuster(_dbContext), _tournamentModule, _currentUserContext, _authorizationService);
        }

        private Match ArrangeFinishedMatch(int editWindowMinutes, bool isOrganizer, params (bool Home, MatchEventType Type)[] goals)
        {
            var tournamentId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Knockout).Value;
            match.Start(Guid.NewGuid());

            var events = goals
                .Select(g => MatchEvent.Create(
                    Guid.NewGuid(), match.Id, g.Type, 10, Guid.NewGuid(),
                    g.Home ? match.HomeTournamentTeamId : match.AwayTournamentTeamId).Value)
                .ToList();
            var home = goals.Count(g => g.Home == (g.Type != MatchEventType.OwnGoal));
            match.UpdateScore(home, goals.Length - home);
            match.Finish(MatchResultType.Regular, null, editWindowMinutes, Guid.NewGuid());
            match.ClearDomainEvents();

            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(events);
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, editWindowMinutes, 3, 1, 0, false, false, 3, 0));
            _authorizationService.HasAnyRoleAsync(
                    Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(isOrganizer);
            return match;
        }

        [Fact]
        public async Task Handle_ShouldAwardWalkover_WithTournamentScores_AndWriteAuditAndOutbox()
        {
            var match = ArrangeFinishedMatch(60, isOrganizer: false, (true, MatchEventType.Goal));
            var command = new ChangeFinishedMatchResultCommand(match.TournamentId, match.Id, MatchResultType.Walkover, match.AwayTournamentTeamId);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(0);
            result.Value.AwayScore.Should().Be(3);
            result.Value.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.Action == "ChangeFinishedResult"), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(
                Arg.Is<OutboxMessage>(m => m.Type.Contains("MatchScoreCorrected")), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldUseGoalsFromEvents_WhenSwitchingToPenalties()
        {
            // Mecz był walkowerem 3:0, a na boisku padło 1:1 (samobój gospodarzy). Wynik do karnych
            // bierze się z goli, nie z zapisanego wyniku walkowera.
            var match = ArrangeFinishedMatch(60, isOrganizer: false, (true, MatchEventType.Goal));
            match.ChangeFinishedResult(MatchResultType.Walkover, match.HomeTournamentTeamId, 1, 0, false, Guid.NewGuid(), 3, 0);
            match.ClearDomainEvents();
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(new[]
            {
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 10, Guid.NewGuid(), match.HomeTournamentTeamId).Value,
                MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.OwnGoal, 20, Guid.NewGuid(), match.HomeTournamentTeamId).Value,
            });
            var command = new ChangeFinishedMatchResultCommand(
                match.TournamentId, match.Id, MatchResultType.Penalties, null, HomePenaltyScore: 2, AwayPenaltyScore: 4);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.HomeScore.Should().Be(1);
            match.AwayScore.Should().Be(1);
            match.WinnerTournamentTeamId.Should().Be(match.AwayTournamentTeamId);
        }

        [Fact]
        public async Task Handle_ShouldRejectReferee_AfterEditWindow()
        {
            var match = ArrangeFinishedMatch(-1, isOrganizer: false, (true, MatchEventType.Goal));
            var command = new ChangeFinishedMatchResultCommand(match.TournamentId, match.Id, MatchResultType.Walkover, match.AwayTournamentTeamId);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.EditLockedAt");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAllowOrganizer_AfterEditWindow()
        {
            var match = ArrangeFinishedMatch(-1, isOrganizer: true, (true, MatchEventType.Goal));
            var command = new ChangeFinishedMatchResultCommand(match.TournamentId, match.Id, MatchResultType.Walkover, match.AwayTournamentTeamId);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            match.ResultType.Should().Be(MatchResultType.Walkover);
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenMatchBelongsToAnotherTournament()
        {
            var match = ArrangeFinishedMatch(60, isOrganizer: true);
            var command = new ChangeFinishedMatchResultCommand(Guid.NewGuid(), match.Id, MatchResultType.Walkover, match.AwayTournamentTeamId);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldRevertGoalWithoutScorerFirst_WhenScoreIsLowered()
        {
            var match = ArrangeFinishedMatch(60, isOrganizer: false, (true, MatchEventType.Goal), (true, MatchEventType.Goal));
            var withScorer = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 30, Guid.NewGuid(), match.HomeTournamentTeamId, Guid.NewGuid()).Value;
            var withoutScorer = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.Goal, 5, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchEventsByMatchIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(new[] { withScorer, withoutScorer });
            _dbContext.GetMatchEventByIdAsync(withScorer.Id, Arg.Any<CancellationToken>()).Returns(withScorer);
            _dbContext.GetMatchEventByIdAsync(withoutScorer.Id, Arg.Any<CancellationToken>()).Returns(withoutScorer);
            var command = new ChangeFinishedMatchResultCommand(
                match.TournamentId, match.Id, MatchResultType.Regular, null, HomeScore: 1, AwayScore: 0);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(1);
            withoutScorer.IsDeleted.Should().BeTrue();
            withScorer.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task Handle_ShouldAddGoalsWithoutScorer_WhenScoreIsRaised()
        {
            var match = ArrangeFinishedMatch(60, isOrganizer: false, (true, MatchEventType.Goal));
            var command = new ChangeFinishedMatchResultCommand(
                match.TournamentId, match.Id, MatchResultType.Regular, null, HomeScore: 2, AwayScore: 1);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.HomeScore.Should().Be(2);
            result.Value.AwayScore.Should().Be(1);
            await _dbContext.Received(2).AddMatchEventAsync(
                Arg.Is<MatchEvent>(e => e.Type == MatchEventType.Goal && e.PlayerId == null), Arg.Any<CancellationToken>());
        }
            // QA v3: PUC-076 - decyzja: organizator zmienia wynik bez limitu czasu, ale tylko do zakończenia turnieju.
        [Theory]
        [InlineData("Finished")]
        [InlineData("Cancelled")]
        public async Task PUC_076_Organizer_CannotChangeResult_AfterTournamentEnded(string status)
        {
            var match = ArrangeFinishedMatch(60, isOrganizer: true, (true, MatchEventType.Goal));
            _tournamentModule.GetTournamentAsync(match.TournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(match.TournamentId, "Cup", status, "Knockout", Guid.NewGuid(), 8));
            var command = new ChangeFinishedMatchResultCommand(match.TournamentId, match.Id, MatchResultType.Walkover, match.AwayTournamentTeamId);

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("The tournament has finished or been cancelled");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
