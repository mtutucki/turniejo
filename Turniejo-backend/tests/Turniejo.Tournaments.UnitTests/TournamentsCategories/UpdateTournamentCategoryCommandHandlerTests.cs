using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentsCategories.UpdateTournamentCategory;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentsCategories
{
    public sealed class UpdateTournamentCategoryCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly UpdateTournamentCategoryCommandHandler _handler;

        public UpdateTournamentCategoryCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _handler = new UpdateTournamentCategoryCommandHandler(_dbContext);
        }

        private static TournamentCategory CreateFakeTournamentCategory()
        {
            var category = TournamentCategory.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Kategoria testowa", null, null, 1).Value;

            category.ClearDomainEvents();
            return category;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            //Arrange
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            var command = new UpdateTournamentCategoryCommand(category.TournamentId, category.Id, "Nowa nazwa", null, null, 2);


            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentCategoryId.Should().Be(category.Id);
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryNotFound()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            _dbContext.GetTournamentCategoryByIdAsync(categoryId, Arg.Any<CancellationToken>())
                .Returns((TournamentCategory?)null);
            var command = new UpdateTournamentCategoryCommand(Guid.NewGuid(), categoryId, "Nazwa", null, null, 1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentCategory.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNameIsInvalid()
        {
            // Arrange
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>())
                .Returns(category);
            var command = new UpdateTournamentCategoryCommand(category.TournamentId, category.Id, string.Empty, null, null, 1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryBelongsToDifferentTournament()
        {
            // Arrange
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>())
                .Returns(category);
            var command = new UpdateTournamentCategoryCommand(Guid.NewGuid(), category.Id, "Nowa nazwa", null, null, 2);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentCategory.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
