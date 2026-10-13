using FluentAssertions;
using NSubstitute;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.Organizations.RenameOrganization;
using Turniejo.Organizations.Domain.Entities;

namespace Turniejo.Organizations.UnitTests.Organizations;

public sealed class RenameOrganizationCommandHandlerTests
{
    private readonly IOrganizationsDbContext _dbContext = Substitute.For<IOrganizationsDbContext>();
    private readonly RenameOrganizationCommandHandler _handler;

    public RenameOrganizationCommandHandlerTests()
    {
        _handler = new RenameOrganizationCommandHandler(_dbContext);
    }

    [Fact]
    public async Task Handle_ShouldRenameAndKeepSlug()
    {
        var organization = Organization.Create(Guid.NewGuid(), "Jan Kowalski").Value;
        var slug = organization.Slug;
        _dbContext.GetOrganizationByIdAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);

        var result = await _handler.Handle(new RenameOrganizationCommand(organization.Id, "  UKS Orły  "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        organization.Name.Should().Be("UKS Orły");
        organization.Slug.Should().Be(slug);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenOrganizationDoesNotExist()
    {
        var result = await _handler.Handle(new RenameOrganizationCommand(Guid.NewGuid(), "UKS Orły"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
