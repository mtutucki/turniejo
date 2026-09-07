using FluentAssertions;
using NSubstitute;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.Organizations.CreateOrganization;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Organizations.UnitTests.Organizations;

public sealed class CreateOrganizationCommandHandlerTests
{
    private readonly IOrganizationsDbContext _dbContext;
    private readonly IUserModule _userModule;
    private readonly CreateOrganizationCommandHandler _handler;

    public CreateOrganizationCommandHandlerTests()
    {
        _dbContext = Substitute.For<IOrganizationsDbContext>();
        _userModule = Substitute.For<IUserModule>();
        _handler = new CreateOrganizationCommandHandler(_dbContext, _userModule);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenOwnerAlreadyHasOrganization()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        _dbContext.OrganizationExistsForOwnerAsync(ownerUserId, Arg.Any<CancellationToken>())
            .Returns(true);
        var command = new CreateOrganizationCommand(ownerUserId, "Moja organizacja");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().AddOrganizationAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenNameIsInvalid()
    {
        // Arrange — pusta nazwa, Organization.Create() powinno zwrócić Failure
        var ownerUserId = Guid.NewGuid();
        _dbContext.OrganizationExistsForOwnerAsync(ownerUserId, Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new CreateOrganizationCommand(ownerUserId, string.Empty);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().AddOrganizationAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenDataIsValid()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        _dbContext.OrganizationExistsForOwnerAsync(ownerUserId, Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new CreateOrganizationCommand(ownerUserId, "Moja organizacja");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.OrganizationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldAddOrganizationAndOutboxMessage_WhenSuccessful()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        _dbContext.OrganizationExistsForOwnerAsync(ownerUserId, Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new CreateOrganizationCommand(ownerUserId, "Moja organizacja");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _dbContext.Received(1).AddOrganizationAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAssignOrganizerRole_WhenSuccessful()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        _dbContext.OrganizationExistsForOwnerAsync(ownerUserId, Arg.Any<CancellationToken>())
            .Returns(false);
        var command = new CreateOrganizationCommand(ownerUserId, "Moja organizacja");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userModule.Received(1).AssignRoleAsync(
            ownerUserId,
            nameof(UserRole.Organizer),
            nameof(RoleScopeType.Organization),
            result.Value.OrganizationId,
            Arg.Any<CancellationToken>());
    }
}