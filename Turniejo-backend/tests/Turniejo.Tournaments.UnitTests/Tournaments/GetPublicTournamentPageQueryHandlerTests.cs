using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.GetPublicTournamentPage;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class GetPublicTournamentPageQueryHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly IMatchModule _matchModule = Substitute.For<IMatchModule>();
    private readonly GetPublicTournamentPageQueryHandler _handler;

    public GetPublicTournamentPageQueryHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _handler = new GetPublicTournamentPageQueryHandler(_dbContext, _matchModule);
        _dbContext.GetTournamentCategoriesByTournamentIdAsync(default, default).ReturnsForAnyArgs(new List<TournamentCategory>());
        _dbContext.GetTournamentGroupsByTournamentIdAsync(default, default).ReturnsForAnyArgs(new List<TournamentGroup>());
        _matchModule.GetPitchesByTournamentIdAsync(default, default).ReturnsForAnyArgs(new List<PitchInfoDto>());
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
        tournament.UpdatePublicSettings(true, true, true, true, true, true, true, true);
        _dbContext.GetTournamentBySlugAsync(tournament.Slug, Arg.Any<CancellationToken>()).Returns(tournament);

        // Act
        var result = await _handler.Handle(new GetPublicTournamentPageQuery(tournament.Slug), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Slug.Should().Be(tournament.Slug);
        result.Value.Name.Should().Be(tournament.Name);
    }
    [Fact]
    public async Task Handle_ShouldIncludeCategoriesGroupsAndPitches_ForPortalFilters()
    {
        var tournament = CreateFakeTournament();
        tournament.UpdatePublicSettings(true, true, true, true, true, true, true, true);
        _dbContext.GetTournamentBySlugAsync(tournament.Slug, Arg.Any<CancellationToken>()).Returns(tournament);
        var u12 = TournamentCategory.Create(Guid.NewGuid(), tournament.OrganizationId, tournament.Id, Guid.NewGuid(), "U12", null, null, 2).Value;
        var u10 = TournamentCategory.Create(Guid.NewGuid(), tournament.OrganizationId, tournament.Id, Guid.NewGuid(), "U10", null, null, 1).Value;
        var groupB = TournamentGroup.Create(Guid.NewGuid(), tournament.OrganizationId, tournament.Id, Guid.NewGuid(), u10.Id, "Grupa B", 2).Value;
        var groupA = TournamentGroup.Create(Guid.NewGuid(), tournament.OrganizationId, tournament.Id, Guid.NewGuid(), u10.Id, "Grupa A", 1).Value;
        _dbContext.GetTournamentCategoriesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(new List<TournamentCategory> { u12, u10 });
        _dbContext.GetTournamentGroupsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(new List<TournamentGroup> { groupB, groupA });
        var pitchId = Guid.NewGuid();
        _matchModule.GetPitchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(new List<PitchInfoDto> { new(pitchId, "Boisko 1", true) });

        var result = await _handler.Handle(new GetPublicTournamentPageQuery(tournament.Slug), CancellationToken.None);

        result.Value.Categories.Select(c => c.Name).Should().Equal("U10", "U12");
        result.Value.Groups.Select(g => g.Name).Should().Equal("Grupa A", "Grupa B");
        result.Value.Groups.Should().OnlyContain(g => g.CategoryId == u10.Id);
        result.Value.Pitches.Should().ContainSingle(p => p.Id == pitchId && p.Name == "Boisko 1");
    }
}
