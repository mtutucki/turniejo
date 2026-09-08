using FluentAssertions;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Events;

namespace Turniejo.Brackets.UnitTests.Entities
{
    public sealed class BracketTests
    {
        [Fact]
        public void Create_ShouldSucceed_AndRaiseBracketGenerated()
        {
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();

            var result = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId);

            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentId.Should().Be(tournamentId);
            result.Value.CategoryId.Should().Be(categoryId);
            result.Value.DomainEvents.Should().ContainSingle(e => e is BracketGenerated);
        }

        [Fact]
        public void Create_ShouldFail_WhenCategoryIdIsEmpty()
        {
            var result = Bracket.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.CategoryId");
        }
    }
}
