using FluentAssertions;
using NSubstitute;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.EventHandlers;
using Turniejo.Organizations.Application.Services;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Exceptions;
using Turniejo.Users.Domain.Events;

namespace Turniejo.Organizations.UnitTests.EventHandlers;

public sealed class UserRegisteredHandlerTests
{
    private readonly IOrganizationsDbContext _dbContext;
    private readonly IUserModule _userModule;
    private readonly UserRegisteredHandler _handler;

    public UserRegisteredHandlerTests()
    {
        _dbContext = Substitute.For<IOrganizationsDbContext>();
        _userModule = Substitute.For<IUserModule>();
        var provisioner = new OrganizationProvisioner(_dbContext);
        _handler = new UserRegisteredHandler(_dbContext, provisioner, _userModule);
    }

    [Fact]
    public async Task Handle_ShouldCreateOrganizationAndAssignOrganizerRole_WhenUserHasNoOrganizationYet()
    {
        // Domyka główny scenariusz z audytu: rejestracja ma faktycznie utworzyć organizację.
        var notification = new UserRegistered(Guid.NewGuid(), "jan@example.com", "Jan", "Kowalski");
        _dbContext.GetOrganizationByOwnerUserIdAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null);
        _dbContext.OrganizationExistsForOwnerAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _handler.Handle(notification, CancellationToken.None);

        await _dbContext.Received(1).AddOrganizationAsync(
            Arg.Is<Organization>(o => o.OwnerUserId == notification.UserId), Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _userModule.Received(1).AssignRoleAsync(
            notification.UserId, nameof(UserRole.Organizer), nameof(RoleScopeType.Organization),
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldOnlyAssignRole_WhenOrganizationAlreadyExists()
    {
        // Retry Outboxa po wcześniejszym częściowym sukcesie (org powstała, rola z jakiegoś powodu nie) —
        // druga dostawa tego samego eventu nie może próbować tworzyć drugiej organizacji.
        var notification = new UserRegistered(Guid.NewGuid(), "jan@example.com", "Jan", "Kowalski");
        var existingOrganization = Organization.Create(notification.UserId, "Jan Kowalski").Value;
        _dbContext.GetOrganizationByOwnerUserIdAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns(existingOrganization);

        await _handler.Handle(notification, CancellationToken.None);

        await _dbContext.DidNotReceive().AddOrganizationAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _userModule.Received(1).AssignRoleAsync(
            notification.UserId, nameof(UserRole.Organizer), nameof(RoleScopeType.Organization),
            existingOrganization.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldRecoverAndAssignRole_WhenCreateRacesWithAnotherOrganizationInsert()
    {
        // Wyścig: między sprawdzeniem GetOrganizationByOwnerUserIdAsync a insertem ktoś inny (retry
        // Outboxa na innej instancji, równoległy POST /api/organizations) już utworzył organizację —
        // unikalny indeks zamieniłby to na DomainException przy realnym SaveChanges, tu symulujemy
        // to samo przez "already exists" z OrganizationProvisioner.
        var notification = new UserRegistered(Guid.NewGuid(), "jan@example.com", "Jan", "Kowalski");
        var raceOrganization = Organization.Create(notification.UserId, "Jan Kowalski").Value;
        _dbContext.GetOrganizationByOwnerUserIdAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null, raceOrganization);
        _dbContext.OrganizationExistsForOwnerAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns(true);

        await _handler.Handle(notification, CancellationToken.None);

        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _userModule.Received(1).AssignRoleAsync(
            notification.UserId, nameof(UserRole.Organizer), nameof(RoleScopeType.Organization),
            raceOrganization.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenCreationFailsForAGenuineReasonAndNoOrganizationExists()
    {
        // Paradoksalny/uszkodzony stan (exists-check mówi "tak", ale wiersza nie da się znaleźć)
        // ma rzucić wyjątek, żeby OutboxProcessor oznaczył wiadomość jako failed/do retry zamiast
        // po cichu ją "przetworzyć" bez faktycznego przypisania roli — dokładnie ten cichy brak,
        // który wykrył audyt.
        var notification = new UserRegistered(Guid.NewGuid(), "jan@example.com", "Jan", "Kowalski");
        _dbContext.GetOrganizationByOwnerUserIdAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns((Organization?)null);
        _dbContext.OrganizationExistsForOwnerAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns(true);

        var act = () => _handler.Handle(notification, CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        await _userModule.DidNotReceive().AssignRoleAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
