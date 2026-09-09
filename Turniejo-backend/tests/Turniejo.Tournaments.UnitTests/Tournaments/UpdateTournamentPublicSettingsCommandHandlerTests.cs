using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournamentPublicSettings;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class UpdateTournamentPublicSettingsCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly UpdateTournamentPublicSettingsCommandHandler _handler;

    public UpdateTournamentPublicSettingsCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _handler = new UpdateTournamentPublicSettingsCommandHandler(_dbContext);
    }

    private static Tournament CreateFakeTournament() =>
        Tournament.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
            8, "Opis testowego turnieju").Value;

    [Fact]
    public async Task Handle_ShouldUpdatePublicSettings_WhenTournamentExists()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var command = new UpdateTournamentPublicSettingsCommand(
            tournament.Id, IsPublic: true, ShowTeams: false, ShowPlayers: true,
            ShowSchedule: true, ShowStandings: false, ShowBracket: true, ShowLiveMatches: false, ShowStats: false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tournament.PublicSettings.IsPublic.Should().BeTrue();
        tournament.PublicSettings.ShowTeams.Should().BeFalse();
        tournament.PublicSettings.ShowStandings.Should().BeFalse();
        tournament.PublicSettings.ShowLiveMatches.Should().BeFalse();
        tournament.PublicSettings.ShowStats.Should().BeFalse();
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTournamentNotFound()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns((Tournament?)null);
        var command = new UpdateTournamentPublicSettingsCommand(tournamentId, true, true, true, true, true, true, true, true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
    }
}
