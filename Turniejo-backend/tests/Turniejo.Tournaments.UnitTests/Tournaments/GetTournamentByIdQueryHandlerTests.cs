using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.GetTournamentById;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class GetTournamentByIdQueryHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly GetTournamentByIdQueryHandler _handler;

    public GetTournamentByIdQueryHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _authorizationService = Substitute.For<IAuthorizationService>();
        _currentUserContext.UserId.Returns(Guid.NewGuid());
        _handler = new GetTournamentByIdQueryHandler(_dbContext, _currentUserContext, _authorizationService);
    }

    private void SetIsOrganizer(bool isOrganizer) =>
        _authorizationService.HasAnyRoleAsync(
                Arg.Any<Guid>(), "Tournament", Arg.Any<Guid>(),
                Arg.Is<IReadOnlyCollection<string>>(r => r.Contains("Organizer")),
                Arg.Any<CancellationToken>())
            .Returns(isOrganizer);

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
        _dbContext.GetTournamentByIdAsync(tournamentId, Arg.Any<CancellationToken>())
            .Returns((Tournament?)null);
        var query = new GetTournamentByIdQuery(tournamentId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tournament.NotFound");
    }

    [Fact]
    public async Task Handle_ShouldReturnDto_WhenTournamentExists()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        var query = new GetTournamentByIdQuery(tournament.Id);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(tournament.Id);
        result.Value.Slug.Should().Be(tournament.Slug);
        result.Value.Description.Should().Be(tournament.Description);
    }

    [Fact]
    public async Task Handle_ShouldReturnAccessCodes_WhenUserIsOrganizer()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        SetIsOrganizer(true);

        // Act
        var result = await _handler.Handle(new GetTournamentByIdQuery(tournament.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.RefereeCode.Should().Be(tournament.RefereeCode);
        result.Value.CoachCode.Should().Be(tournament.CoachCode);
        result.Value.ViewerCode.Should().Be(tournament.ViewerCode);
    }

    [Fact]
    public async Task Handle_ShouldHideAccessCodes_WhenUserIsNotOrganizer()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        _dbContext.GetTournamentByIdAsync(tournament.Id, Arg.Any<CancellationToken>())
            .Returns(tournament);
        SetIsOrganizer(false);

        // Act
        var result = await _handler.Handle(new GetTournamentByIdQuery(tournament.Id), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be(tournament.Name);
        result.Value.RefereeCode.Should().BeNull();
        result.Value.CoachCode.Should().BeNull();
        result.Value.ViewerCode.Should().BeNull();
    }


}