using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentsCategories
{
    public sealed class DeleteTournamentCategoryCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly DeleteTournamentCategoryCommandHandler _handler;

        public DeleteTournamentCategoryCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _handler = new DeleteTournamentCategoryCommandHandler(_dbContext);
        }

        private static TournamentCategory CreateFakeTournamentCategory()
        {
            var tournamentCategory = TournamentCategory.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "kategoria testowa", null, null, 1)
                .Value;

            tournamentCategory.ClearDomainEvents();
            return tournamentCategory;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            //Arrange
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryNotFound()
        {
            //Arrange
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns((TournamentCategory?)null);
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryBelongsToDifferentTournament()
        {
            //Arrange
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            var command = new DeleteTournamentCategoryCommand(Guid.NewGuid(), category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentCategory.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
