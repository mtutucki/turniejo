using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Users.Application.Abstractions;
using Turniejo.Users.Application.Users.UpdateMyProfile;
using Turniejo.Users.Domain.Entities;

namespace Turniejo.Users.UnitTests.Users;

public sealed class UpdateMyProfileCommandHandlerTests
{
    private readonly IKeycloakAdminService _keycloak = Substitute.For<IKeycloakAdminService>();
    private readonly IUsersDbContext _dbContext = Substitute.For<IUsersDbContext>();
    private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();
    private readonly User _user = User.Create(Guid.NewGuid(), "jan@turniejo.pl", "Jan", "Kowalski", "500100200").Value;
    private readonly UpdateMyProfileCommandHandler _handler;

    public UpdateMyProfileCommandHandlerTests()
    {
        _currentUser.UserId.Returns(_user.Id);
        _dbContext.GetUserByIdAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
        _handler = new UpdateMyProfileCommandHandler(_dbContext, _keycloak, _currentUser);
    }

    [Fact]
    public async Task Handle_ShouldUpdateProfileAndKeycloakName()
    {
        var result = await _handler.Handle(new UpdateMyProfileCommand(" Janusz ", "Nowak", "  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _user.FirstName.Should().Be("Janusz");
        _user.LastName.Should().Be("Nowak");
        _user.PhoneNumber.Should().BeNull();
        _user.Email.Should().Be("jan@turniejo.pl");
        await _keycloak.Received(1).UpdateNameAsync(_user.Id, "Janusz", "Nowak", Arg.Any<CancellationToken>());
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotSave_WhenKeycloakFails()
    {
        _keycloak.UpdateNameAsync(_user.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Keycloak down"));

        var act = () => _handler.Handle(new UpdateMyProfileCommand("Janusz", "Nowak", null), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReject_WhenNameIsBlank()
    {
        var result = await _handler.Handle(new UpdateMyProfileCommand("Jan", " ", null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.LastName");
        await _keycloak.DidNotReceive().UpdateNameAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
