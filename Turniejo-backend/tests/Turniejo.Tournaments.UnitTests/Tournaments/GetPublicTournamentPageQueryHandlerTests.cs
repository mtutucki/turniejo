using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.GetPublicTournamentPage;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class GetPublicTournamentPageQueryHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly GetPublicTournamentPageQueryHandler _handler;

    public GetPublicTournamentPageQueryHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _handler = new GetPublicTournamentPageQueryHandler(_dbContext);
    }

    private static Tournament CreateFakeTournament() =>
        Tournament.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
            8, "Opis testowego turnieju").Value;

    [Fact]
    public async Task Handle_ShouldFail_WhenTournamentNotFound()
    {
        // Arrange
        _dbContext.GetTournamentBySlugAsync("unknown-slug", Arg.Any<CancellationToken>()).Returns((Tournament?)null);

        // Act
        var result = await _handler.Handle(new GetPublicTournamentPageQuery("unknown-slug"), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTournamentIsNotPublic()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentBySlugAsync(tournament.Slug, Arg.Any<CancellationToken>()).Returns(tournament);

        // Act
        var result = await _handler.Handle(new GetPublicTournamentPageQuery(tournament.Slug), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnPage_WhenTournamentIsPublic()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        tournament.UpdatePublicSettings(true, true, true, true, true, true, true);
        _dbContext.GetTournamentBySlugAsync(tournament.Slug, Arg.Any<CancellationToken>()).Returns(tournament);

        // Act
        var result = await _handler.Handle(new GetPublicTournamentPageQuery(tournament.Slug), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be(tournament.Slug);
        result.Value.Name.Should().Be(tournament.Name);
    }
}
