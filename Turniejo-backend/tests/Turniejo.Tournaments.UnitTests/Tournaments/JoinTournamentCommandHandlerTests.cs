using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.JoinTournament;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class JoinTournamentCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserModule _userModule;
    private readonly JoinTournamentCommandHandler _handler;

    public JoinTournamentCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _userModule = Substitute.For<IUserModule>();

        _handler = new JoinTournamentCommandHandler(
            _dbContext, _currentUserContext, _userModule);
    }

    private static Tournament CreateFakeTournament() =>
        Tournament.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(1),
            DateTimeOffset.UtcNow.AddDays(2),
            8,
            null).Value;

    [Fact]
    public async Task Handle_ShouldFail_WhenCodeIsInvalid()
    {
        // Arrange
        _dbContext.GetTournamentByCodeAsync("INVALID", Arg.Any<CancellationToken>())
            .Returns((Tournament?)null);

        var command = new JoinTournamentCommand("INVALID");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Code");
    }

    [Fact]
    public async Task Handle_ShouldAssignRefereeRole_WhenRefereeCodeMatches()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        var userId = Guid.NewGuid();

        _dbContext.GetTournamentByCodeAsync(tournament.RefereeCode, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(userId);

        var command = new JoinTournamentCommand(tournament.RefereeCode);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userModule.Received(1).AssignRoleAsync(
            userId,
            nameof(UserRole.Referee),
            nameof(RoleScopeType.Tournament),
            tournament.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignCoachRole_WhenCoachCodeMatches()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        var userId = Guid.NewGuid();

        _dbContext.GetTournamentByCodeAsync(tournament.CoachCode, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(userId);

        var command = new JoinTournamentCommand(tournament.CoachCode);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userModule.Received(1).AssignRoleAsync(
            userId,
            nameof(UserRole.Coach),
            nameof(RoleScopeType.Tournament),
            tournament.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignViewerRole_WhenViewerCodeMatches()
    {
        // Arrange
        var tournament = CreateFakeTournament();
        var userId = Guid.NewGuid();

        _dbContext.GetTournamentByCodeAsync(tournament.ViewerCode, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(userId);

        var command = new JoinTournamentCommand(tournament.ViewerCode);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _userModule.Received(1).AssignRoleAsync(
            userId,
            nameof(UserRole.Viewer),
            nameof(RoleScopeType.Tournament),
            tournament.Id,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnTournamentAndRole_WhenCodeIsValid()
    {
        // Arrange
        var tournament = CreateFakeTournament();

        _dbContext.GetTournamentByCodeAsync(tournament.CoachCode, Arg.Any<CancellationToken>())
            .Returns(tournament);
        _currentUserContext.UserId.Returns(Guid.NewGuid());

        var command = new JoinTournamentCommand(tournament.CoachCode);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Value.Slug.Should().Be(tournament.Slug);
        result.Value.TournamentId.Should().Be(tournament.Id);
        result.Value.TournamentName.Should().Be(tournament.Name);
        result.Value.Role.Should().Be("Coach");
    }
}