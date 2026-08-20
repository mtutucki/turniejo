using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentsCategories.GetTournamentCategories;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentsCategories
{
    public sealed class GetTournamentCategoriesQueryHandlerTests
    {
        private readonly ITournamentsDbContext _context;
        private readonly GetTournamentCategoriesQueryHandler _handler;

        public GetTournamentCategoriesQueryHandlerTests()
        {
            _context = Substitute.For<ITournamentsDbContext>();
            _handler = new GetTournamentCategoriesQueryHandler(_context);
        }

        private static TournamentCategory CreateFakeTournamentCategory()
        {
            return TournamentCategory.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "kategoria", null, null, 1)
                .Value;
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoTournamentGroupsExist()
        {
            // Arrange
            Guid tournamentId = Guid.NewGuid();
            _context.GetTournamentCategoriesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentCategory>());
            var query = new GetTournamentCategoriesQuery(tournamentId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenTournamentGroupExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var tournamentCategory = CreateFakeTournamentCategory();
            _context.GetTournamentCategoriesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentCategory> { tournamentCategory });
            var query = new GetTournamentCategoriesQuery(tournamentId);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(tournamentCategory.Id);
            result.Value[0].TournamentId.Should().Be(tournamentCategory.TournamentId);
            result.Value[0].Name.Should().Be(tournamentCategory.Name);
            result.Value[0].MinBirthDate.Should().Be(tournamentCategory.MinBirthDate);
            result.Value[0].MaxBirthDate.Should().Be(tournamentCategory.MaxBirthDate);
            result.Value[0].Order.Should().Be(tournamentCategory.Order);
        }
    }
}
