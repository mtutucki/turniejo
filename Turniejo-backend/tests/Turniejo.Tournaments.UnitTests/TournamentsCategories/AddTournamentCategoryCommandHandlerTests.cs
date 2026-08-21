using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentsCategories.AddTournamentCategory;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentsCategories
{
    public sealed class AddTournamentCategoryCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddTournamentCategoryCommandHandler _handler;

        public AddTournamentCategoryCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new AddTournamentCategoryCommandHandler(_dbContext, _currentUserContext);
        }

        private static Tournament CreateFakeTournament()
        {
            var tournament = Tournament.Create(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Turniej testowy",
                DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
                8, null).Value;

            tournament.ClearDomainEvents();
            return tournament;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
            var command = new AddTournamentCategoryCommand(tournament.Id, "U12", null, null, 1);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentCategoryId.Should().NotBeEmpty();
            await _dbContext.Received(1).AddTournamentCategoryAsync(Arg.Any<TournamentCategory>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns((Tournament?)null);
            var command = new AddTournamentCategoryCommand(tournamentId, "Category 1", null, null, 1);
            // Act
            var result = await _handler.Handle(command, CancellationToken.None);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryNameAlreadyExists()
        {
            // Arrange
            var tournament = CreateFakeTournament();
            const string categoryName = "U12";
            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _dbContext.CategoryNameExistsAsync(tournament.Id, categoryName, Arg.Any<CancellationToken>())
                .Returns(true);
            var command = new AddTournamentCategoryCommand(tournament.Id, categoryName, null, null, 1);
            // Act
            var result = await _handler.Handle(command, CancellationToken.None);
            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            result.Error.Message.Should().Be($"Tournament category with name '{categoryName}' already exists");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
        {
            //Arrange
            var tournament = CreateFakeTournament();
            _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
                .Returns(tournament);
            _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
            var command = new AddTournamentCategoryCommand(tournament.Id, "Category 1", null, null, 1);

            //Act
            await _handler.Handle(command, CancellationToken.None);

            //Assert
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }
    }
}