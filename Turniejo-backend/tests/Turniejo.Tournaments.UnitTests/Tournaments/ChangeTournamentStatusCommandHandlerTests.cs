using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.ChangeTournamentStatus;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;
// using Turniejo.Tournaments.Domain.Enums; ← dopasuj namespace TournamentStatus u siebie

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class ChangeTournamentStatusCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ChangeTournamentStatusCommandHandler _handler;

    public ChangeTournamentStatusCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _handler = new ChangeTournamentStatusCommandHandler(_dbContext, _currentUserContext);
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
        var command = new ChangeTournamentStatusCommand(tournament.Id, TournamentStatus.ScheduleGenerated);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Format");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}