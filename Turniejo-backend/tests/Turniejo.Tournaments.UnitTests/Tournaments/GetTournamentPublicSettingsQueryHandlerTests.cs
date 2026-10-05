using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentPublicSettings;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class GetTournamentPublicSettingsQueryHandlerTests
{
    private readonly ITournamentsDbContext _dbContext = Substitute.For<ITournamentsDbContext>();
    private readonly GetTournamentPublicSettingsQueryHandler _handler;

    public GetTournamentPublicSettingsQueryHandlerTests()
    {
        _handler = new GetTournamentPublicSettingsQueryHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldReturnCurrentSettings()
    {
        var tournament = Tournament.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Turniej", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), 8, null).Value;
        tournament.UpdatePublicSettings(true, false, true, true, false, true, true, false);
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var result = await _handler.Handle(new GetTournamentPublicSettingsQuery(tournament.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsPublic.Should().BeTrue();
        result.Value.ShowTeams.Should().BeFalse();
        result.Value.ShowStandings.Should().BeFalse();
        result.Value.ShowStats.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTournamentNotFound()
    {
        var id = Guid.NewGuid();
        _dbContext.GetTournamentByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Tournament?)null);

        var result = await _handler.Handle(new GetTournamentPublicSettingsQuery(id), CancellationToken.None);

        result.Error.Code.Should().Be("Tournament.NotFound");
    }
}
