using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentGroups.AddTournamentGroup;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentGroups
{
    public sealed class AddTournamentGroupCommandHandlerTests
    {
        private readonly ITournamentsDbContext _context;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AddTournamentGroupCommandHandler _handler;

        public AddTournamentGroupCommandHandlerTests()
        {
            _context = Substitute.For<ITournamentsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new AddTournamentGroupCommandHandler(_context, _currentUserContext);
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

        private static TournamentCategory CreateFakeTournamentCategory(Guid categoryId, Guid tournamentId)
        {
            var category = TournamentCategory.Create(
                categoryId, Guid.NewGuid(), tournamentId, Guid.NewGuid(),
                "Kategoria testowa", null, null, 1).Value;

            category.ClearDomainEvents();
            return category;
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            //Arrange
            var tournament = CreateFakeTournament();
            var tournamentCategory = CreateFakeTournamentCategory(Guid.NewGuid(), tournament.Id);
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
            _context.GetTournamentCategoryByIdAsync(tournamentCategory.Id, Arg.Any<CancellationToken>()).Returns(tournamentCategory);
            _context.GroupNameExistsAsync(tournamentCategory.Id, "name", Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddTournamentGroupCommand(tournament.Id, tournamentCategory.Id, "name", 1);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.TournamentGroupId.Should().NotBeEmpty();
            await _context.Received(1).AddTournamentGroupAsync(Arg.Any<TournamentGroup>(), Arg.Any<CancellationToken>());
            await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
        {
            //Arrange
            var tournament = CreateFakeTournament();
            var tournamentCategory = CreateFakeTournamentCategory(Guid.NewGuid(), tournament.Id);
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
            _context.GetTournamentCategoryByIdAsync(tournamentCategory.Id, Arg.Any<CancellationToken>()).Returns(tournamentCategory);
            _context.GroupNameExistsAsync(tournamentCategory.Id, "name", Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddTournamentGroupCommand(tournament.Id, tournamentCategory.Id, "name", 1);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            await _context.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFound()
        {
            //Arrange
            Guid invalidTournamentId = Guid.NewGuid();
            var tournamentCategory = CreateFakeTournamentCategory(Guid.NewGuid(), invalidTournamentId);
            _context.GetTournamentByIdAsync(invalidTournamentId, Arg.Any<CancellationToken>()).Returns((Tournament?)null);
            _context.GetTournamentCategoryByIdAsync(tournamentCategory.Id, Arg.Any<CancellationToken>()).Returns(tournamentCategory);
            _context.GroupNameExistsAsync(tournamentCategory.Id, "name", Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddTournamentGroupCommand(invalidTournamentId, tournamentCategory.Id, "name", 1);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
            await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryNotFound()
        {
            //Arrange
            var tournament = CreateFakeTournament();
            Guid invalidCategoryId = Guid.NewGuid();
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
            _context.GetTournamentCategoryByIdAsync(invalidCategoryId, Arg.Any<CancellationToken>()).Returns((TournamentCategory?)null);
            _context.GroupNameExistsAsync(invalidCategoryId, "name", Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddTournamentGroupCommand(tournament.Id, invalidCategoryId, "name", 1);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentCategory.NotFound");
            await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryBelongsToDifferentTournament()
        {
            //Arrange
            var tournament = CreateFakeTournament();
            Guid otherTournamentId = Guid.NewGuid();
            var tournamentCategory = CreateFakeTournamentCategory(Guid.NewGuid(), otherTournamentId);
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
            _context.GetTournamentCategoryByIdAsync(tournamentCategory.TournamentId, Arg.Any<CancellationToken>()).Returns(tournamentCategory);
            _context.GroupNameExistsAsync(tournamentCategory.Id, "name", Arg.Any<CancellationToken>()).Returns(false);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddTournamentGroupCommand(tournament.Id, tournamentCategory.TournamentId, "name", 1);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.TournamentId");
            await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupNameAlreadyExists()
        {
            //Arrange
            var tournament = CreateFakeTournament();
            var tournamentCategory = CreateFakeTournamentCategory(Guid.NewGuid(), tournament.Id);
            _context.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
            _context.GetTournamentCategoryByIdAsync(tournamentCategory.Id, Arg.Any<CancellationToken>()).Returns(tournamentCategory);
            _context.GroupNameExistsAsync(tournamentCategory.Id, "name", Arg.Any<CancellationToken>()).Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddTournamentGroupCommand(tournament.Id, tournamentCategory.Id, "name", 1);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
