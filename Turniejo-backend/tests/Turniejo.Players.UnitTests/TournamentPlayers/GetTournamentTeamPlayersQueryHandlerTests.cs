using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class GetTournamentTeamPlayersQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly GetTournamentTeamPlayersQueryHandler _handler;

        public GetTournamentTeamPlayersQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _handler = new GetTournamentTeamPlayersQueryHandler(_dbContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenPlayersExist()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { tournamentPlayer });

            // Act
            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(Guid.NewGuid(), tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(tournamentPlayer.Id);
            result.Value[0].ShirtNumber.Should().Be(9);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoPlayersExist()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer>());

            // Act
            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(Guid.NewGuid(), tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
    }
}
