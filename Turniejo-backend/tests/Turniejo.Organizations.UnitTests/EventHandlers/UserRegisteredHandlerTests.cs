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
    public async Task Handle_ShouldNameOrganizationAsGivenAtRegistration()
    {
        var notification = new UserRegistered(Guid.NewGuid(), "jan@example.com", "Jan", "Kowalski", OrganizationName: " UKS Orły ");

        await _handler.Handle(notification, CancellationToken.None);

        await _dbContext.Received(1).AddOrganizationAsync(
            Arg.Is<Organization>(o => o.Name == "UKS Orły"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNameOrganizationAfterOwner_WhenNoNameWasGiven()
    {
        var notification = new UserRegistered(Guid.NewGuid(), "jan@example.com", "Jan", "Kowalski");

        await _handler.Handle(notification, CancellationToken.None);

        await _dbContext.Received(1).AddOrganizationAsync(
            Arg.Is<Organization>(o => o.Name == "Jan Kowalski"), Arg.Any<CancellationToken>());
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

    [Fact]
    public async Task Handle_ShouldSkipOrganizationAndRole_WhenUserRegisteredAsParticipant()
    {
        // Rodzic/trener/sędzia wybrał przy rejestracji "Dołączam do turnieju" — nie zostaje organizatorem.
        var notification = new UserRegistered(Guid.NewGuid(), "rodzic@example.com", "Anna", "Nowak", CreateOrganization: false);

        await _handler.Handle(notification, CancellationToken.None);

        await _dbContext.DidNotReceive().AddOrganizationAsync(Arg.Any<Organization>(), Arg.Any<CancellationToken>());
        await _userModule.DidNotReceive().AssignRoleAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void UserRegistered_ShouldDefaultToCreatingOrganization_ForOutboxMessagesWithoutTheFlag()
    {
        // Wiadomości zapisane w Outboxie przed dodaniem flagi nie mają tego pola.
        var json = """{"UserId":"6f1c2c9e-4c55-4d0f-9d1e-2a3b4c5d6e7f","Email":"jan@example.com","FirstName":"Jan","LastName":"Kowalski"}""";

        var notification = System.Text.Json.JsonSerializer.Deserialize<UserRegistered>(json)!;

        notification.CreateOrganization.Should().BeTrue();
    }
}
