using FluentAssertions;
using Turniejo.SharedKernel.Results;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.Entities
{
    public sealed class TieBreakerRuleTests
    {
        private static readonly Guid validId = Guid.NewGuid();
        private const int validOrder = 1;
        private const TieBreakerType validTieBreakerType = TieBreakerType.HeadToHeadPoints;

        private static Result<TieBreakerRule> CreateValdTieBreakerRule()
        {
            return TieBreakerRule.Create(validId, Guid.NewGuid(), validOrder, validTieBreakerType);
        }

        [Fact]
        public void Create_ShouldSucceed_WhenAllDataIsValid()
        {
            //Act
            var result = CreateValdTieBreakerRule();
            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(validId);
            result.Value.Order.Should().Be(validOrder);
            result.Value.Type.Should().Be(validTieBreakerType);
        }

        [Fact]
        public void Create_ShouldFail_WhenIdIsEmpty()
        {
            //Act
            var result = TieBreakerRule.Create(Guid.Empty, Guid.NewGuid(), validOrder, validTieBreakerType);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Id");
            result.Error.Message.Should().Be("TieBreakerRule id cannot be empty");
        }

        [Fact]
        public void Create_ShouldFail_WhenTournamentIdIsEmpty()
        {
            //Act
            var result = TieBreakerRule.Create(validId, Guid.Empty, validOrder, validTieBreakerType);
            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
            result.Error.Message.Should().Be("TieBreakerRule tournamentId cannot be empty");
        }
    }
}
