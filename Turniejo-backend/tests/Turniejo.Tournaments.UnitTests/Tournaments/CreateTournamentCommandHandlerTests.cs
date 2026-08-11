using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Tournaments.CreateTournament;
using Turniejo.Tournaments.Domain.Entities;

namespace Turniejo.Tournaments.UnitTests.Tournaments;

public sealed class CreateTournamentCommandHandlerTests
{
    private readonly ITournamentsDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserModule _userModule;
    private readonly CreateTournamentCommandHandler _handler;

    public CreateTournamentCommandHandlerTests()
    {
        _dbContext = Substitute.For<ITournamentsDbContext>();
        _currentUserContext = Substitute.For<ICurrentUserContext>();
        _userModule = Substitute.For<IUserModule>();
        _handler = new CreateTournamentCommandHandler(_dbContext, _currentUserContext, _userModule);
    }

    private static CreateTournamentCommand ValidCommand(Guid organizationId) =>
        new(organizationId, "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2),
            8, null);

    [Fact]
    public async Task Handle_ShouldFail_WhenTournamentDataIsInvalid()
    {
        // Arrange — EndAt przed StartAt, Tournament.Create() powinno zwrócić Failure
        var command = new CreateTournamentCommand(
            Guid.NewGuid(), "Turniej testowy",
            DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(1),
            8, null);
        _currentUserContext.UserId.Returns(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().AddTournamentAsync(Arg.Any<Tournament>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenSlugAlreadyExists()
    {
        // Arrange
        var command = ValidCommand(Guid.NewGuid());
        _currentUserContext.UserId.Returns(Guid.NewGuid());
        _dbContext.TournamentSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("409");
        await _dbContext.DidNotReceive().AddTournamentAsync(Arg.Any<Tournament>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDataIsValidAndSlugIsUnique()
    {
        // Arrange
        _currentUserContext.UserId.Returns(Guid.NewGuid());
        var command = ValidCommand(Guid.NewGuid());
        _dbContext.TournamentSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TournamentId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAddTournamentAndOutboxMessage_WhenSuccessful()
    {
        // Arrange
        _currentUserContext.UserId.Returns(Guid.NewGuid());
        var command = ValidCommand(Guid.NewGuid());
        _dbContext.TournamentSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddTournamentAsync(Arg.Any<Tournament>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignOrganizerRole_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUserContext.UserId.Returns(userId);
        var command = ValidCommand(Guid.NewGuid());
        _dbContext.TournamentSlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userModule.Received(1).AssignRoleAsync(
            userId, nameof(UserRole.Organizer), nameof(RoleScopeType.Tournament),
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}