using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TieBreakerRules.SetTieBreakerRules;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.TieBreakerRules
{
    public sealed class SetTieBreakerRulesCommandHandlerTests
    {
        private readonly ITournamentsDbContext _context;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly SetTieBreakerRulesCommandHandler _handler;

        public SetTieBreakerRulesCommandHandlerTests()
        {
            _context = Substitute.For<ITournamentsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new SetTieBreakerRulesCommandHandler(_context, _currentUserContext);
        }

        private static Tournament CreateFakeTournament()
        {
            var tournament = Tournament.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Turniej testowy",
                DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
                8, null).Value;

            tournament.ClearDomainEvents();
            return tournament;
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _context.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns((Tournament?)null);
            var command = new SetTieBreakerRulesCommand(
                tournamentId,
                new List<TieBreakerRuleItem> { new(1, TieBreakerType.HeadToHeadPoints) });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
            await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenOrderValuesAreDuplicated()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            var command = new SetTieBreakerRulesCommand(
                tournament.Id,
                new List<TieBreakerRuleItem>
                {
                    new(1, TieBreakerType.HeadToHeadPoints),
                    new(1, TieBreakerType.GoalDifference)
                });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Order");
            await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new SetTieBreakerRulesCommand(
                tournament.Id,
                new List<TieBreakerRuleItem>
                {
                    new(1, TieBreakerType.HeadToHeadPoints),
                    new(2, TieBreakerType.GoalDifference),
                    new(3, TieBreakerType.GoalsFor)
                });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.RulesCount.Should().Be(3);
            await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReplaceExistingRules_WhenSuccessful()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new SetTieBreakerRulesCommand(
                tournament.Id,
                new List<TieBreakerRuleItem> { new(1, TieBreakerType.HeadToHeadPoints) });

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _context.Received(1).ReplaceTieBreakerRulesAsync(
                tournament.Id,
                Arg.Is<IReadOnlyList<TieBreakerRule>>(list => list.Count == 1),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new SetTieBreakerRulesCommand(
                tournament.Id,
                new List<TieBreakerRuleItem> { new(1, TieBreakerType.HeadToHeadPoints) });

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _context.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }
    }
}