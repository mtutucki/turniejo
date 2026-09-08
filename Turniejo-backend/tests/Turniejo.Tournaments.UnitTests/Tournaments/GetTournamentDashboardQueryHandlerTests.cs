using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentDashboard;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class GetTournamentDashboardQueryHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ITeamModule _teamModule;
    private readonly IMatchModule _matchModule;
    private readonly GetTournamentDashboardQueryHandler _handler;

    public GetTournamentDashboardQueryHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _teamModule = Substitute.For<ITeamModule>();
        _matchModule = Substitute.For<IMatchModule>();
        _handler = new GetTournamentDashboardQueryHandler(_dbContext, _teamModule, _matchModule);
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
        var tournamentId = Guid.NewGuid();
        _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns((Tournament?)null);
        var query = new GetTournamentDashboardQuery(tournamentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldAggregateTeamCounts_ByRegistrationStatus()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var teams = new List<TournamentTeamDto>
        {
            new(Guid.NewGuid(), tournament.Id, Guid.NewGuid(), Guid.NewGuid(), null, "Pending"),
            new(Guid.NewGuid(), tournament.Id, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"),
            new(Guid.NewGuid(), tournament.Id, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"),
            new(Guid.NewGuid(), tournament.Id, Guid.NewGuid(), Guid.NewGuid(), null, "Rejected"),
            new(Guid.NewGuid(), tournament.Id, Guid.NewGuid(), Guid.NewGuid(), null, "Withdrawn"),
        };
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(teams);
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentMatchDto>());

        var query = new GetTournamentDashboardQuery(tournament.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Teams.Pending.Should().Be(1);
        result.Value.Teams.Accepted.Should().Be(2);
        result.Value.Teams.Rejected.Should().Be(1);
        result.Value.Teams.Withdrawn.Should().Be(1);
        result.Value.Teams.Total.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ShouldReturnNotGeneratedScheduleStatus_WhenNoMatchesExist()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentTeamDto>());
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentMatchDto>());

        var query = new GetTournamentDashboardQuery(tournament.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduleStatus.Should().Be("NotGenerated");
        result.Value.Matches.Total.Should().Be(0);
        result.Value.UpcomingMatches.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnCompletedScheduleStatus_WhenAllMatchesFinished()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentTeamDto>());
        var matches = new List<TournamentMatchDto>
        {
            new(Guid.NewGuid(), tournament.Id, "Finished", DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), 0, 0),
            new(Guid.NewGuid(), tournament.Id, "Finished", DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), 0, 0),
        };
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(matches);

        var query = new GetTournamentDashboardQuery(tournament.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduleStatus.Should().Be("Completed");
        result.Value.Matches.Finished.Should().Be(2);
        result.Value.Matches.Scheduled.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnInProgressScheduleStatus_WhenSomeMatchesFinishedAndSomeNot()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentTeamDto>());
        var matches = new List<TournamentMatchDto>
        {
            new(Guid.NewGuid(), tournament.Id, "Finished", DateTimeOffset.UtcNow.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), 0, 0),
            new(Guid.NewGuid(), tournament.Id, "Scheduled", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid(), Guid.NewGuid(), 0, 0),
        };
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(matches);

        var query = new GetTournamentDashboardQuery(tournament.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduleStatus.Should().Be("InProgress");
    }

    [Fact]
    public async Task Handle_ShouldReturnGeneratedScheduleStatus_WhenMatchesExistButNoneFinished()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentTeamDto>());
        var matches = new List<TournamentMatchDto>
        {
            new(Guid.NewGuid(), tournament.Id, "Scheduled", DateTimeOffset.UtcNow.AddDays(1), Guid.NewGuid(), Guid.NewGuid(), 0, 0),
        };
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(matches);

        var query = new GetTournamentDashboardQuery(tournament.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduleStatus.Should().Be("Generated");
    }

    [Fact]
    public async Task Handle_ShouldReturnUpcomingMatches_OrderedByScheduledAt_WithResolvedTeamNames()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentTeamDto>());

        var homeTournamentTeamId = Guid.NewGuid();
        var awayTournamentTeamId = Guid.NewGuid();
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        var laterMatchId = Guid.NewGuid();
        var earlierMatchId = Guid.NewGuid();
        var matches = new List<TournamentMatchDto>
        {
            new(laterMatchId, tournament.Id, "Scheduled", DateTimeOffset.UtcNow.AddDays(5), homeTournamentTeamId, awayTournamentTeamId, 0, 0),
            new(earlierMatchId, tournament.Id, "Ready", DateTimeOffset.UtcNow.AddDays(1), homeTournamentTeamId, awayTournamentTeamId, 0, 0),
            new(Guid.NewGuid(), tournament.Id, "Finished", DateTimeOffset.UtcNow.AddDays(-1), homeTournamentTeamId, awayTournamentTeamId, 0, 0),
        };
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(matches);

        _teamModule.GetTournamentTeamAsync(homeTournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(homeTournamentTeamId, tournament.Id, homeTeamId, Guid.NewGuid(), null, "Accepted"));
        _teamModule.GetTournamentTeamAsync(awayTournamentTeamId, Arg.Any<CancellationToken>())
            .Returns(new TournamentTeamDto(awayTournamentTeamId, tournament.Id, awayTeamId, Guid.NewGuid(), null, "Accepted"));
        _teamModule.GetTeamAsync(homeTeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(homeTeamId, "Lechia", "LEC"));
        _teamModule.GetTeamAsync(awayTeamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(awayTeamId, "Legia", "LEG"));

        var query = new GetTournamentDashboardQuery(tournament.Id, UpcomingMatchesCount: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingMatches.Should().HaveCount(2);
        result.Value.UpcomingMatches[0].MatchId.Should().Be(earlierMatchId);
        result.Value.UpcomingMatches[1].MatchId.Should().Be(laterMatchId);
        result.Value.UpcomingMatches[0].HomeTeamName.Should().Be("Lechia");
        result.Value.UpcomingMatches[0].AwayTeamName.Should().Be("Legia");
    }

    [Fact]
    public async Task Handle_ShouldLimitUpcomingMatches_ToRequestedCount()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(new List<TournamentTeamDto>());
        _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);

        var matches = Enumerable.Range(0, 3)
            .Select(i => new TournamentMatchDto(Guid.NewGuid(), tournament.Id, "Scheduled", DateTimeOffset.UtcNow.AddDays(i + 1), Guid.NewGuid(), Guid.NewGuid(), 0, 0))
            .ToList();
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(matches);

        var query = new GetTournamentDashboardQuery(tournament.Id, UpcomingMatchesCount: 2);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UpcomingMatches.Should().HaveCount(2);
    }
}
