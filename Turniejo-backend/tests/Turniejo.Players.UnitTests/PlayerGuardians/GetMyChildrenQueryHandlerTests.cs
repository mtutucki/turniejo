using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.PlayerGuardians.GetMyChildren;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.PlayerGuardians
{
    public sealed class GetMyChildrenQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetMyChildrenQueryHandler _handler;

        public GetMyChildrenQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new GetMyChildrenQueryHandler(_dbContext, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedChildren_WhenGuardianshipsExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, "photo.png", null, Guid.NewGuid()).Value;
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, userId, RelationshipType.Parent, true, true, Guid.NewGuid()).Value;

            _currentUserContext.UserId.Returns(userId);
            _dbContext.GetGuardianshipsByUserIdAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian> { guardian });
            _dbContext.GetPlayersByIdsAsync(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(player.Id)), Arg.Any<CancellationToken>())
                .Returns(new List<Player> { player });

            // Act
            var result = await _handler.Handle(new GetMyChildrenQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].PlayerId.Should().Be(player.Id);
            result.Value[0].CanEdit.Should().BeTrue();
            result.Value[0].RelationshipType.Should().Be(RelationshipType.Parent.ToString());
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoGuardianshipsExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);
            _dbContext.GetGuardianshipsByUserIdAsync(userId, Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());

            // Act
            var result = await _handler.Handle(new GetMyChildrenQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
