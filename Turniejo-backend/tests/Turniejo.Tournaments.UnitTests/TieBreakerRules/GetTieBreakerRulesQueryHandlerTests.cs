using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TieBreakerRules.GetTieBreakerRules;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.TieBreakerRules
{
    public sealed class GetTieBreakerRulesQueryHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly GetTieBreakerRulesQueryHandler _handler;

        public GetTieBreakerRulesQueryHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _handler = new GetTieBreakerRulesQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoRulesExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetTieBreakerRulesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TieBreakerRule>());

            // Act
            var result = await _handler.Handle(new GetTieBreakerRulesQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnRulesSortedByOrder_WithTypeNames()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var rules = new List<TieBreakerRule>
            {
                TieBreakerRule.Create(Guid.NewGuid(), tournamentId, 2, TieBreakerType.GoalsFor).Value,
                TieBreakerRule.Create(Guid.NewGuid(), tournamentId, 1, TieBreakerType.HeadToHeadPoints).Value,
            };
            _dbContext.GetTieBreakerRulesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(rules);

            // Act
            var result = await _handler.Handle(new GetTieBreakerRulesQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Select(r => r.Type).Should().Equal("HeadToHeadPoints", "GoalsFor");
            result.Value.Select(r => r.Order).Should().Equal(1, 2);
        }
    }
}
