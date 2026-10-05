using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.ResetBracket;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class ResetBracketCommandHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly IMatchModule _matchModule;
        private readonly ResetBracketCommandHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();

        public ResetBracketCommandHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _matchModule = Substitute.For<IMatchModule>();
            _handler = new ResetBracketCommandHandler(_dbContext, _tournamentModule, _matchModule);
        }

        private void SetupTournament(string status, string format = "Knockout") =>
            _tournamentModule.GetTournamentAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(_tournamentId, "Test", status, format, Guid.NewGuid(), 16));

        // Drabinka z jedną parą 1. rundy, która ma już mecz.
        private (Bracket Bracket, Guid MatchId) SetupBracketWithMatch()
        {
            var bracket = Bracket.Create(Guid.NewGuid(), _tournamentId, _categoryId).Value;
            var round = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var node = BracketNode.Create(
                Guid.NewGuid(), round.Id,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;
            var matchId = Guid.NewGuid();
            node.AssignMatch(matchId);

            _dbContext.GetBracketByTournamentAndCategoryAsync(_tournamentId, _categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);

            return (bracket, matchId);
        }

        [Fact]
        public async Task Handle_ShouldRemoveMatchesAndBracket_AndRevertStage_ForKnockoutWithGeneratedSchedule()
        {
            // Arrange
            SetupTournament("ScheduleGenerated");
            var (bracket, matchId) = SetupBracketWithMatch();
            _matchModule.RemoveUnstartedMatchesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(true);
            _tournamentModule.RevertScheduleGenerationAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var result = await _handler.Handle(new ResetBracketCommand(_tournamentId, _categoryId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchesRemoved.Should().Be(1);
            await _matchModule.Received(1).RemoveUnstartedMatchesAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == matchId), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemoveBracketAsync(bracket, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
            await _tournamentModule.Received(1).RevertScheduleGenerationAsync(_tournamentId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotRevertStage_WhenScheduleNotGeneratedYet()
        {
            // Arrange
            SetupTournament("RegistrationClosed");
            var (bracket, _) = SetupBracketWithMatch();
            _matchModule.RemoveUnstartedMatchesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(true);

            // Act
            var result = await _handler.Handle(new ResetBracketCommand(_tournamentId, _categoryId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemoveBracketAsync(bracket, Arg.Any<CancellationToken>());
            await _tournamentModule.DidNotReceive().RevertScheduleGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnConflict_AndKeepBracket_WhenAMatchHasStarted()
        {
            // Arrange
            SetupTournament("InProgress");
            SetupBracketWithMatch();
            _matchModule.RemoveUnstartedMatchesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(false);

            // Act
            var result = await _handler.Handle(new ResetBracketCommand(_tournamentId, _categoryId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Cannot reset the bracket after one of its matches has started");
            await _dbContext.DidNotReceive().RemoveBracketAsync(Arg.Any<Bracket>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenBracketDoesNotExist()
        {
            // Arrange
            SetupTournament("RegistrationClosed");
            _dbContext.GetBracketByTournamentAndCategoryAsync(_tournamentId, _categoryId, Arg.Any<CancellationToken>()).Returns((Bracket?)null);

            // Act
            var result = await _handler.Handle(new ResetBracketCommand(_tournamentId, _categoryId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            await _matchModule.DidNotReceive().RemoveUnstartedMatchesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }
    }
}
