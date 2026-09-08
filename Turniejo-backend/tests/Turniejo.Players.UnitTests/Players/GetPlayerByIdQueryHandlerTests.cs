using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Players.GetPlayerById;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Players
{
    public sealed class GetPlayerByIdQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetPlayerByIdQueryHandler _handler;

        public GetPlayerByIdQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _dbContext.GetTournamentPlayersByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns([]);
            var playerAccessGuard = new PlayerAccessGuard(_dbContext, _teamModule, _authorizationService);
            _handler = new GetPlayerByIdQueryHandler(_dbContext, playerAccessGuard, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDto_WhenCallerIsLinkedToPlayer()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, "photo.png", userId, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _currentUserContext.UserId.Returns(userId);

            // Act
            var result = await _handler.Handle(new GetPlayerByIdQuery(player.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Id.Should().Be(player.Id);
            result.Value.FirstName.Should().Be(player.FirstName);
            result.Value.DefaultPosition.Should().Be(player.DefaultPosition.ToString());
        }

        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenPlayerNotExist()
        {
            // Arrange
            var playerId = Guid.NewGuid();
            _dbContext.GetPlayerByIdAsync(playerId, Arg.Any<CancellationToken>()).Returns((Player?)null);

            // Act
            var result = await _handler.Handle(new GetPlayerByIdQuery(playerId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Player.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenCallerHasNoRelationToPlayer()
        {
            // Zamyka IDOR z audytu: sama znajomość PlayerId nie wystarczy — brak samego konta,
            // opiekuństwa albo roli Organizer/Coach w drużynie zawodnika ma teraz kończyć się 403,
            // nawet gdy zawodnik nie ma jeszcze żadnego opiekuna.
            var player = Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _dbContext.GetPlayerByIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns(player);
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([]);
            _currentUserContext.UserId.Returns(Guid.NewGuid());

            // Act
            var act = () => _handler.Handle(new GetPlayerByIdQuery(player.Id), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
