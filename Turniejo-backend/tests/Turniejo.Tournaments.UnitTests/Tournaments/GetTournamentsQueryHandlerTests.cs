using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.GetTournaments;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class GetTournamentsQueryHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly GetTournamentsQueryHandler _handler;

    public GetTournamentsQueryHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _handler = new GetTournamentsQueryHandler(_dbContext);
    }

    private static Tournament CreateFakeTournament(Guid organizationId) =>
        Tournament.Create(
            Guid.NewGuid(), organizationId, Guid.NewGuid(),
            "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
            8, null).Value;

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoTournamentsExist()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        _dbContext.GetTournamentsByOrganizationIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new List<Tournament>());
        var query = new GetTournamentsQuery(organizationId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnMappedDtos_WhenTournamentsExist()
    {
        // Arrange
        var organizationId = Guid.NewGuid();
        var tournament = CreateFakeTournament(organizationId);
        _dbContext.GetTournamentsByOrganizationIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(new List<Tournament> { tournament });
        var query = new GetTournamentsQuery(organizationId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(tournament.Id);
        result.Value[0].Slug.Should().Be(tournament.Slug);
        result.Value[0].Status.Should().Be(tournament.Status.ToString());
    }
}