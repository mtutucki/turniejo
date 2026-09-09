using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.GetPublicBracket;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class GetPublicBracketQueryHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicBracketQueryHandler _handler;

        public GetPublicBracketQueryHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicBracketQueryHandler(_dbContext, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showBracket) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Knockout",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, showBracket, true, true));

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            // Arrange
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            // Act
            var result = await _handler.Handle(new GetPublicBracketQuery("unknown", Guid.NewGuid()), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenShowBracketIsFalse()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showBracket: false));

            // Act
            var result = await _handler.Handle(new GetPublicBracketQuery("cup-slug", categoryId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Bracket.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnBracket_WhenShowBracketIsTrueAndBracketExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showBracket: true));

            var bracket = Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value;
            var round = BracketRound.Create(Guid.NewGuid(), bracket.Id, 1, BracketRoundType.Standard, "Final").Value;
            var node = BracketNode.Create(
                Guid.NewGuid(), round.Id,
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid(),
                BracketSourceType.Fixed, null, null, null, Guid.NewGuid()).Value;

            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>()).Returns(bracket);
            _dbContext.GetBracketRoundsByBracketIdAsync(bracket.Id, Arg.Any<CancellationToken>()).Returns([round]);
            _dbContext.GetBracketNodesByRoundIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([node]);

            // Act
            var result = await _handler.Handle(new GetPublicBracketQuery("cup-slug", categoryId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Rounds.Should().ContainSingle();
            result.Value.Rounds[0].Nodes.Should().ContainSingle(n => n.Id == node.Id);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenBracketDoesNotExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showBracket: true));
            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>())
                .Returns((Bracket?)null);

            // Act
            var result = await _handler.Handle(new GetPublicBracketQuery("cup-slug", categoryId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Bracket.NotFound");
        }
    }
}
