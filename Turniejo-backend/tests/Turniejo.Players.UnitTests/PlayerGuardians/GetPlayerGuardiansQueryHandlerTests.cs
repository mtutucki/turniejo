using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.PlayerGuardians.GetPlayerGuardians;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.PlayerGuardians
{
    public sealed class GetPlayerGuardiansQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly GetPlayerGuardiansQueryHandler _handler;

        public GetPlayerGuardiansQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _handler = new GetPlayerGuardiansQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenGuardiansExist()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), playerId, Guid.NewGuid(), RelationshipType.Parent, true, false, Guid.NewGuid()).Value;
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian> { guardian });

            // Act
            var result = await _handler.Handle(new GetPlayerGuardiansQuery(playerId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(guardian.Id);
            result.Value[0].CanEdit.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoGuardiansExist()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(playerId, Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());

            // Act
            var result = await _handler.Handle(new GetPlayerGuardiansQuery(playerId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
