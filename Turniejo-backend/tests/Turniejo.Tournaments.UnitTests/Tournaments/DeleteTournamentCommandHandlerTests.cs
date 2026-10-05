using FluentAssertions;
using NSubstitute;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.DeleteTournament;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class DeleteTournamentCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext = Substitute.For<ITournamentsDbContext>();
    private readonly DeleteTournamentCommandHandler _handler;

    public DeleteTournamentCommandHandlerTests() => _handler = new DeleteTournamentCommandHandler(_dbContext);

    private static Tournament TournamentIn(TournamentStatus status)
    {
        var tournament = Tournament.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Turniej",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2), 8, null).Value;
        if (status != TournamentStatus.Draft)
            tournament.ChangeStatus(status, Guid.NewGuid()).IsSuccess.Should().BeTrue();
        tournament.ClearDomainEvents();
        return tournament;
    }

    [Fact]
    public async Task Handle_ShouldSoftDelete_CancelledTournament()
    {
        var tournament = TournamentIn(TournamentStatus.Cancelled);
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var result = await _handler.Handle(new DeleteTournamentCommand(tournament.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        tournament.DeletedAt.Should().NotBeNull();
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TournamentStatus.Draft)]
    [InlineData(TournamentStatus.RegistrationOpen)]
    public async Task Handle_ShouldRefuse_TournamentThatIsNotCancelled(TournamentStatus status)
    {
        // Trwający czy zakończony turniej ma wyniki i historię — usunąć można tylko anulowany.
        var tournament = TournamentIn(status);
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>()).Returns(tournament);

        var result = await _handler.Handle(new DeleteTournamentCommand(tournament.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Status");
        tournament.DeletedAt.Should().BeNull();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenTournamentMissing()
    {
        var id = Guid.NewGuid();
        _dbContext.GetTournamentByIdAsync(id, Arg.Any<CancellationToken>()).Returns((Tournament?)null);

        var result = await _handler.Handle(new DeleteTournamentCommand(id), CancellationToken.None);

        result.Error.Code.Should().Be("Tournament.NotFound");
    }
}
