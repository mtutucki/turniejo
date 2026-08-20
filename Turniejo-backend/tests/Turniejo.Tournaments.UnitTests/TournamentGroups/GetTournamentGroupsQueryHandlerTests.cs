using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentGroups.GetTournamentGroups;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentGroups
{
    public sealed class GetTournamentGroupsQueryHandlerTests
    {
        private readonly ITournamentsDbContext _context;
        private readonly GetTournamentGroupsQueryHandler _handler;

        public GetTournamentGroupsQueryHandlerTests()
        {
            _context = Substitute.For<ITournamentsDbContext>();
            _handler = new GetTournamentGroupsQueryHandler(_context);
        }

        private static TournamentGroup CreateFakeTournamentGroup()
        {
            return TournamentGroup.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "grupa", 1)
                .Value;
        }


        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoGroupsExist()
        {
            // Arrange
            Guid tournamentId = Guid.NewGuid();
            _context.GetTournamentGroupsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                 .Returns(new List<TournamentGroup>());
            var query = new GetTournamentGroupsQuery(tournamentId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenGroupsExist()
        {
            // Arrange
            var tournamentGroup = CreateFakeTournamentGroup();
            Guid tournamentId = Guid.NewGuid();
            _context.GetTournamentGroupsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                 .Returns(new List<TournamentGroup> { tournamentGroup });
            var query = new GetTournamentGroupsQuery(tournamentId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(tournamentGroup.Id);
            result.Value[0].TournamentId.Should().Be(tournamentGroup.TournamentId);
            result.Value[0].TournamentId.Should().Be(tournamentGroup.TournamentId);
            result.Value[0].CategoryId.Should().Be(tournamentGroup.CategoryId);
            result.Value[0].Name.Should().Be(tournamentGroup.Name);
            result.Value[0].Order.Should().Be(tournamentGroup.Order);
        }
    }
}
