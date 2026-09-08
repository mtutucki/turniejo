using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.GetBracket;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class GetBracketQueryHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly GetBracketQueryHandler _handler;

        public GetBracketQueryHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _handler = new GetBracketQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnBracketWithRoundsAndNodes_WhenBracketExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var bracket = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value;
            var round = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Round 1").Value;
            var node = BracketNode.Create(
                Guid.NewGuid(), round.Id,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;

            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);

            // Act
            var result = await _handler.Handle(new GetBracketQuery(tournamentId, categoryId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Rounds.Should().ContainSingle();
            result.Value.Rounds[0].Nodes.Should().ContainSingle(n => n.Id == node.Id);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenBracketNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>())
                .Returns((Bracket?)null);

            // Act
            var result = await _handler.Handle(new GetBracketQuery(tournamentId, categoryId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Bracket.NotFound");
        }
    }
}
