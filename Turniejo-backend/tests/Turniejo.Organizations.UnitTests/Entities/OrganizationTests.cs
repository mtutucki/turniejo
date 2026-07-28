using FluentAssertions;
using Turniejo.Organizations.Domain.Entities;

namespace Turniejo.Organizations.UnitTests.Entities;

public sealed class OrganizationTests
{
    private static Guid ValidOwnerUserId => Guid.NewGuid();
    private const string ValidName = "Jan Kowalski FC";


    [Fact]
    public void Create_ShouldSucceed_WhenDataIsValid()
    {
        var ownerId = ValidOwnerUserId;

        var result = Organization.Create(ownerId, ValidName);

        result.IsSuccess.Should().BeTrue();
        result.Value.OwnerUserId.Should().Be(ownerId);
        result.Value.Name.Should().Be(ValidName);
    }

    [Fact]
    public void Create_ShouldGenerateSlug()
    {
        var result = Organization.Create(ValidOwnerUserId, ValidName);

        result.Value.Slug.Should().NotBeNullOrEmpty();
        result.Value.Slug.Should().Contain("jan-kowalski-fc");
    }

    [Fact]
    public void Create_ShouldGenerateUniqueSlug()
    {
        var result1 = Organization.Create(ValidOwnerUserId, ValidName);
        var result2 = Organization.Create(ValidOwnerUserId, ValidName);

        result1.Value.Slug.Should().NotBe(result2.Value.Slug);
    }

    [Fact]
    public void Create_ShouldGenerateUniqueId()
    {
        var result1 = Organization.Create(ValidOwnerUserId, ValidName);
        var result2 = Organization.Create(ValidOwnerUserId, ValidName);

        result1.Value.Id.Should().NotBe(result2.Value.Id);
    }

    [Fact]
    public void Create_ShouldHandlePolishCharactersInSlug()
    {
        var result = Organization.Create(ValidOwnerUserId, "Łódź FC");

        result.Value.Slug.Should().Contain("lodz-fc");
    }

    // ── Walidacja OwnerUserId ─────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldFail_WhenOwnerUserIdIsEmpty()
    {
        var result = Organization.Create(Guid.Empty, ValidName);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.OwnerUserId");
    }

    // ── Walidacja Name ────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldFail_WhenNameIsEmpty()
    {
        var result = Organization.Create(ValidOwnerUserId, string.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Name");
    }

    [Fact]
    public void Create_ShouldFail_WhenNameIsWhitespace()
    {
        var result = Organization.Create(ValidOwnerUserId, "   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Name");
    }
}