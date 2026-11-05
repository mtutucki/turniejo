using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.TournamentsCategories.DeleteTournamentCategory;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.TournamentsCategories
{
    public sealed class DeleteTournamentCategoryCommandHandlerTests
    {
        private readonly ITournamentsDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IMatchModule _matchModule;
        private readonly IPlayerModule _playerModule;
        private readonly IUserModule _userModule;
        private readonly DeleteTournamentCategoryCommandHandler _handler;

        public DeleteTournamentCategoryCommandHandlerTests()
        {
            _dbContext = Substitute.For<ITournamentsDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _matchModule = Substitute.For<IMatchModule>();
            _playerModule = Substitute.For<IPlayerModule>();
            _userModule = Substitute.For<IUserModule>();
            _handler = new DeleteTournamentCategoryCommandHandler(_dbContext, _teamModule, _matchModule, _playerModule, _userModule);
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
            _teamModule.AnyActiveTeamsInCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(false);
            _matchModule.AnyMatchesForCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(false);
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldRemoveCategoryGroups_WhenDeletingCategory()
        {
            // Arrange
            var category = CreateFakeTournamentCategory();
            var groupA = TournamentGroup.Create(Guid.NewGuid(), Guid.NewGuid(), category.TournamentId, Guid.NewGuid(), category.Id, "Grupa A", 1).Value;
            var groupB = TournamentGroup.Create(Guid.NewGuid(), Guid.NewGuid(), category.TournamentId, Guid.NewGuid(), category.Id, "Grupa B", 2).Value;
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            _dbContext.GetTournamentGroupsByCategoryIdAsync(category.Id, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentGroup> { groupA, groupB });
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemoveTournamentGroupAsync(groupA, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemoveTournamentGroupAsync(groupB, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemoveTournamentCategoryAsync(category, Arg.Any<CancellationToken>());
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

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryStillHasRegisteredTeams()
        {
            // Zamyka lukę z audytu: CategoryId nie ma FK do Teams, więc kasowanie kategorii cicho
            // osierocało TournamentTeam.CategoryId w innym module.
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            _teamModule.AnyActiveTeamsInCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(true);
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.CategoryId");
            await _dbContext.DidNotReceive().RemoveTournamentCategoryAsync(Arg.Any<TournamentCategory>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenCategoryStillHasMatches()
        {
            // Ten sam brak co przy drużynach, tylko po stronie Matches — Match.CategoryId też nie ma FK.
            var category = CreateFakeTournamentCategory();
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            _teamModule.AnyActiveTeamsInCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(false);
            _matchModule.AnyMatchesForCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(true);
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.CategoryId");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldRemoveRejectedAndWithdrawnTeams_WhenDeletingCategory()
        {
            // Lista drużyn ukrywa odrzucone i wycofane, więc kategoria wygląda na pustą — znikają razem z nią,
            // łącznie ze składami i rolami.
            var category = CreateFakeTournamentCategory();
            var inactiveTeamIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var removedPlayerIds = new[] { Guid.NewGuid() };
            _dbContext.GetTournamentCategoryByIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(category);
            _teamModule.AnyActiveTeamsInCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(false);
            _matchModule.AnyMatchesForCategoryAsync(category.Id, Arg.Any<CancellationToken>()).Returns(false);
            _teamModule.GetInactiveTournamentTeamIdsByCategoryIdAsync(category.Id, Arg.Any<CancellationToken>()).Returns(inactiveTeamIds);
            _playerModule.RemovePlayersOfTournamentTeamsAsync(inactiveTeamIds, Arg.Any<CancellationToken>()).Returns(removedPlayerIds);
            var command = new DeleteTournamentCategoryCommand(category.TournamentId, category.Id);

            //Act
            var result = await _handler.Handle(command, CancellationToken.None);

            //Assert
            result.IsSuccess.Should().BeTrue();
            await _userModule.Received(1).RemoveRolesByScopesAsync("TournamentTeam", inactiveTeamIds, Arg.Any<CancellationToken>());
            await _userModule.Received(1).RemoveRolesByScopesAsync("Player", removedPlayerIds, Arg.Any<CancellationToken>());
            await _teamModule.Received(1).RemoveTournamentTeamsAsync(inactiveTeamIds, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemoveTournamentCategoryAsync(category, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
