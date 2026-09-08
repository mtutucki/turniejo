using FluentAssertions;
using NSubstitute;
using Turniejo.Organizations.Application.Abstractions;
using Turniejo.Organizations.Application.Organizations.GetMyOrganization;
using Turniejo.Organizations.Domain.Entities;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Organizations.UnitTests.Organizations
{
    public sealed class GetMyOrganizationQueryHandlerTests
    {
        private readonly IOrganizationsDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetMyOrganizationQueryHandler _handler;

        public GetMyOrganizationQueryHandlerTests()
        {
            _dbContext = Substitute.For<IOrganizationsDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new GetMyOrganizationQueryHandler(_dbContext, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldReturnOrganization_WhenUserOwnsOne()
        {
            // Zamyka lukę z audytu: brakujący GET dla organizacji zalogowanego usera.
            var userId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);
            var organization = Organization.Create(userId, "Klub Sportowy Testowy").Value;
            _dbContext.GetOrganizationByOwnerUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(organization);

            // Act
            var result = await _handler.Handle(new GetMyOrganizationQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().NotBeNull();
            result.Value!.Id.Should().Be(organization.Id);
            result.Value.Slug.Should().Be(organization.Slug);
        }

        [Fact]
        public async Task Handle_ShouldReturnNull_WhenUserHasNoOrganizationYet()
        {
            // Brak organizacji to legalny stan (np. tuż po rejestracji), nie błąd.
            var userId = Guid.NewGuid();
            _currentUserContext.UserId.Returns(userId);
            _dbContext.GetOrganizationByOwnerUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns((Organization?)null);

            // Act
            var result = await _handler.Handle(new GetMyOrganizationQuery(), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeNull();
        }
    }
}
