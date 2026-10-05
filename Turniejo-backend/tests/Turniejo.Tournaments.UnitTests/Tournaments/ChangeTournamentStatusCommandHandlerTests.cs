using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class ChangeTournamentStatusCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IMatchModule _matchModule;
    private readonly ChangeTournamentStatusCommandHandler _handler;

    public ChangeTournamentStatusCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _matchModule = Substitute.For<IMatchModule>();
        _handler = new ChangeTournamentStatusCommandHandler(_dbContext, _currentUserContext, _matchModule);
    }

    private static Tournament CreateTournamentInProgress()
    {
        var tournament = CreateFakeTournament();
        tournament.SetFormat(TournamentFormat.Group);
        foreach (var status in new[]
                 {
                     TournamentStatus.RegistrationOpen, TournamentStatus.RegistrationClosed,
                     TournamentStatus.ScheduleGenerated, TournamentStatus.InProgress
                 })
            tournament.ChangeStatus(status, tournament.CreatedByUserId);

        tournament.ClearDomainEvents();
        return tournament;
    }

    private static TournamentMatchDto MatchWithStatus(Guid tournamentId, string status) =>
        new(Guid.NewGuid(), tournamentId, status, DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), 0, 0);

    [Theory]
    [InlineData("Live")]
    [InlineData("Paused")]
    public async Task Handle_ShouldRejectFinishing_WhenMatchIsInProgress(string matchStatus)
    {
        // Trwający mecz zmieniłby tabelę i zwycięzcę już po zakończeniu turnieju.
        var tournament = CreateTournamentInProgress();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns([MatchWithStatus(tournament.Id, "Finished"), MatchWithStatus(tournament.Id, matchStatus)]);

        var result = await _handler.Handle(new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.Finished), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict.TournamentStatus");
        tournament.Status.Should().Be(TournamentStatus.InProgress);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAllowFinishing_WhenOnlyUnplayedMatchesRemain()
    {
        // Nierozpoczęte mecze nie blokują — moduł Matches anuluje je po zmianie statusu.
        var tournament = CreateTournamentInProgress();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns([MatchWithStatus(tournament.Id, "Finished"), MatchWithStatus(tournament.Id, "Scheduled")]);

        var result = await _handler.Handle(new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.Finished), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tournament.Status.Should().Be(TournamentStatus.Finished);
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
    public async Task Handle_ShouldFail_WhenTournamentNotFound()
    {
        // Arrange
        var tournamentId = Guid.NewGuid();
        _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns((Tournament?)null);
        var command = new ChangeTournamentStatusCommand(tournamentId, TournamentStatus.RegistrationOpen);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTransitionIsIllegal()
    {
        // Arrange — świeży turniej (Draft) nie powinien móc przeskoczyć od razu do Finished
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
        var command = new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.Finished);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenTransitionIsLegal()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
        var command = new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.RegistrationOpen);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TournamentId.Should().Be(tournament.Id);
        result.Value.Slug.Should().Be(tournament.Slug);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
        var command = new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.RegistrationOpen);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTransitioningToScheduleGeneratedWithoutFormat()
    {
        // Arrange — turniej bez ustawionego formatu, przeprowadzony do RegistrationClosed
        var tournament = CreateFakeTournament();
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen, tournament.CreatedByUserId);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed, tournament.CreatedByUserId);

        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);
        var command = new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.ScheduleGenerated, IsScheduleTransition: true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Format");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRejectManualScheduleGenerated_EvenWhenTransitionIsOtherwiseLegal()
    {
        // Ręczne ustawienie zostawiłoby "harmonogram gotowy" bez meczów, a generator nie działa poza RegistrationClosed.
        var tournament = CreateFakeTournament();
        tournament.SetFormat(TournamentFormat.Group);
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen, tournament.CreatedByUserId);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed, tournament.CreatedByUserId);
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _currentUserContext.UserId.Returns(tournament.CreatedByUserId);

        var manual = await _handler.Handle(new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.ScheduleGenerated), CancellationToken.None);

        manual.IsFailure.Should().BeTrue();
        manual.Error.Code.Should().Be("Validation.TournamentStatus");
        tournament.Status.Should().Be(TournamentStatus.RegistrationClosed);

        var fromGenerator = await _handler.Handle(
            new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.ScheduleGenerated, IsScheduleTransition: true), CancellationToken.None);

        fromGenerator.IsSuccess.Should().BeTrue();
        tournament.Status.Should().Be(TournamentStatus.ScheduleGenerated);
    }
}