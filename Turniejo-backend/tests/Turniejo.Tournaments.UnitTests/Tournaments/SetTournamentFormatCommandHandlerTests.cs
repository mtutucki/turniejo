using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Outbox;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Services;
using Turniejo.Tournaments.Application.Tournaments.SetTournamentFormat;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class SetTournamentFormatCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ITeamModule _teamModule;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly SetTournamentFormatCommandHandler _handler;

    public SetTournamentFormatCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _teamModule = Substitute.For<ITeamModule>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _handler = new SetTournamentFormatCommandHandler(
            _dbContext, new DefaultGroupProvisioner(_dbContext, _teamModule, _currentUserContext));
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
        var command = new SetTournamentFormatCommand(tournamentId, TournamentFormat.Group);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenFormatIsValid()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        var command = new SetTournamentFormatCommand(tournament.Id, TournamentFormat.Knockout);

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
        var command = new SetTournamentFormatCommand(tournament.Id, TournamentFormat.Knockout);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
    }
}