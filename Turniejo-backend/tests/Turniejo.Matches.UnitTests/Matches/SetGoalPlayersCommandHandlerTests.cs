using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.SetGoalPlayers;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class SetGoalPlayersCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
        private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
        private readonly SetGoalPlayersCommandHandler _handler;
        private readonly Guid _scorer = Guid.NewGuid();
        private readonly Guid _assist = Guid.NewGuid();

        public SetGoalPlayersCommandHandlerTests()
        {
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            _handler = new SetGoalPlayersCommandHandler(
                _dbContext, new MatchEventRosterGuard(_playerModule), _currentUserContext, _authorizationService);
        }

        private (Match Match, MatchEvent Goal) Arrange(
            bool finished, int editWindowMinutes = 10, bool isOrganizer = false, MatchEventType type = MatchEventType.Goal)
        {
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.Start(Guid.NewGuid());
            if (finished)
                match.Finish(MatchResultType.Regular, null, editWindowMinutes, Guid.NewGuid());
            match.ClearDomainEvents();

            var goal = MatchEvent.Create(Guid.NewGuid(), match.Id, type, 0, Guid.NewGuid(), match.HomeTournamentTeamId).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _dbContext.GetMatchEventByIdAsync(goal.Id, Arg.Any<CancellationToken>()).Returns(goal);
            _playerModule.GetTournamentTeamPlayersAsync(match.HomeTournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new[] { _scorer, _assist }
                    .Select(id => new TournamentTeamPlayerDto(Guid.NewGuid(), match.HomeTournamentTeamId, id, "Jan", "Kowalski", null, null, "Active"))
                    .ToList());
            _authorizationService.HasAnyRoleAsync(
                    Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(isOrganizer);
            return (match, goal);
        }

        [Fact]
        public async Task Handle_ShouldSetScorerAndAssist_WithoutChangingTheGoal()
        {
            var (match, goal) = Arrange(finished: true);

            var result = await _handler.Handle(
                new SetGoalPlayersCommand(match.TournamentId, match.Id, goal.Id, _scorer, _assist), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            goal.PlayerId.Should().Be(_scorer);
            goal.RelatedPlayerId.Should().Be(_assist);
            goal.Type.Should().Be(MatchEventType.Goal);
            await _dbContext.Received(1).AddAuditLogAsync(
                Arg.Is<AuditLog>(a => a.Action == "SetGoalPlayers"), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldLetOrganizerSetScorer_AfterEditWindowClosed()
        {
            var (match, goal) = Arrange(finished: true, editWindowMinutes: 0, isOrganizer: true);

            var result = await _handler.Handle(
                new SetGoalPlayersCommand(match.TournamentId, match.Id, goal.Id, _scorer, null), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            goal.PlayerId.Should().Be(_scorer);
        }

        [Fact]
        public async Task Handle_ShouldFail_ForRefereeAfterEditWindowClosed()
        {
            var (match, goal) = Arrange(finished: true, editWindowMinutes: 0, isOrganizer: false);

            var result = await _handler.Handle(
                new SetGoalPlayersCommand(match.TournamentId, match.Id, goal.Id, _scorer, null), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
            goal.PlayerId.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenPlayerIsNotOnTheGoalTeamRoster()
        {
            var (match, goal) = Arrange(finished: false);

            var result = await _handler.Handle(
                new SetGoalPlayersCommand(match.TournamentId, match.Id, goal.Id, Guid.NewGuid(), null), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PlayerId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenOwnGoalGetsAnAssist()
        {
            var (match, goal) = Arrange(finished: false, type: MatchEventType.OwnGoal);

            var result = await _handler.Handle(
                new SetGoalPlayersCommand(match.TournamentId, match.Id, goal.Id, _scorer, _assist), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.AssistPlayerId");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenEventIsNotAGoal()
        {
            var (match, _) = Arrange(finished: false);
            var card = MatchEvent.Create(Guid.NewGuid(), match.Id, MatchEventType.YellowCard, 5, Guid.NewGuid(), match.HomeTournamentTeamId, _scorer).Value;
            _dbContext.GetMatchEventByIdAsync(card.Id, Arg.Any<CancellationToken>()).Returns(card);

            var result = await _handler.Handle(
                new SetGoalPlayersCommand(match.TournamentId, match.Id, card.Id, _assist, null), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("MatchEvent.NotFound");
        }
    }
}
