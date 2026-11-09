using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.UpdateTournament;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class UpdateTournamentDetailsCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ITeamModule _teamModule;
    private readonly IMatchModule _matchModule = Substitute.For<IMatchModule>();
    private readonly UpdateTournamentDetailsCommandHandler _handler;

    public UpdateTournamentDetailsCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _teamModule = Substitute.For<ITeamModule>();
        _handler = new UpdateTournamentDetailsCommandHandler(_dbContext, _teamModule, _matchModule);
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
        var command = new UpdateTournamentDetailsCommand(
            tournamentId, "Nowa nazwa",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
            10, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenDetailsAreInvalid()
    {
        // Arrange — EndAt przed StartAt
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Nowa nazwa",
            DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(1),
            10, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDetailsAreValid()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Nowa nazwa",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(3),
            12, "Opis");

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
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Nowa nazwa",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(3),
            12, "Opis");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }

    private void SetupAcceptedTeams(Guid tournamentId, int accepted)
    {
        var teams = Enumerable.Range(0, accepted)
            .Select(_ => new TournamentTeamDto(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"))
            .Append(new TournamentTeamDto(Guid.NewGuid(), tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Pending"))
            .ToList();
        _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns(teams);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenLimitIsLoweredBelowAcceptedTeams()
    {
        // Arrange — limit 8, zaakceptowane 4, próba zmiany na 3
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        SetupAcceptedTeams(tournament.Id, 4);
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Nowa nazwa",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(3),
            3, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.TeamLimit");
        tournament.TeamLimit.Should().Be(8);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenLimitIsLoweredToAcceptedTeams()
    {
        // Arrange — oczekujące się nie liczą, limit równy zaakceptowanym jest w porządku
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        SetupAcceptedTeams(tournament.Id, 4);
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Nowa nazwa",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(3),
            4, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tournament.TeamLimit.Should().Be(4);
    }

    // QA v3: UST-004 - decyzja: zapis dat blokowany, gdy jakiś mecz wypadłby poza nowym zakresem.
    private Tournament ArrangeWithMatchAt(DateTimeOffset matchAt, string status = "Scheduled")
    {
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        _matchModule.GetMatchesByTournamentIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns([new TournamentMatchDto(Guid.NewGuid(), tournament.Id, status, matchAt, Guid.NewGuid(), Guid.NewGuid(), 0, 0)]);
        return tournament;
    }

    [Fact]
    public async Task UST_004_ShorteningDates_BelowAScheduledMatch_IsRejected()
    {
        var tournament = ArrangeWithMatchAt(DateTimeOffset.UtcNow.AddDays(2).AddHours(-1));
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, tournament.Name, tournament.StartAt, tournament.StartAt.AddHours(3), 8, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Be("Some matches fall outside the new tournament dates");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UST_004_ChangingDates_ThatStillContainAllMatches_IsSaved()
    {
        var tournament = ArrangeWithMatchAt(DateTimeOffset.UtcNow.AddDays(1).AddHours(1));
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, tournament.Name, tournament.StartAt, tournament.StartAt.AddHours(5), 8, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UST_004_CancelledMatchOutsideDates_DoesNotBlock()
    {
        var tournament = ArrangeWithMatchAt(DateTimeOffset.UtcNow.AddDays(2).AddHours(-1), "Cancelled");
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, tournament.Name, tournament.StartAt, tournament.StartAt.AddHours(3), 8, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    // QA v3: UST-015 - decyzja: zapis drugiego organizatora bez odświeżenia nie może po cichu
    // nadpisać zmian pierwszego. Formularz wysyła UpdatedAt z chwili wczytania.
    [Fact]
    public async Task UST_015_SavingOverSomeoneElsesChange_IsRejected()
    {
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        var loadedAt = tournament.UpdatedAt.AddMinutes(-5);
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Inna nazwa", tournament.StartAt, tournament.EndAt, 8, null, ExpectedUpdatedAt: loadedAt);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict.UpdatedAt");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UST_015_SavingWithTheCurrentVersion_IsAccepted()
    {
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);
        var command = new UpdateTournamentDetailsCommand(
            tournament.Id, "Inna nazwa", tournament.StartAt, tournament.EndAt, 8, null, ExpectedUpdatedAt: tournament.UpdatedAt);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}
