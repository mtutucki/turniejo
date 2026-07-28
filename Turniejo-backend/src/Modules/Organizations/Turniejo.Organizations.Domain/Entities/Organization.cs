using Turniejo.SharedKernel.Primitives;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Organizations.Domain.Entities;

public sealed class Organization : BaseEntity
{
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    private Organization(Guid id) : base(id) { }
    private Organization() { }

    public static Result<Organization> Create(
        Guid ownerUserId,
        string name)
    {
        if (ownerUserId == Guid.Empty)
            return Result.Failure<Organization>(
                Error.Validation("OwnerUserId", "OwnerUserId cannot be empty."));

        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<Organization>(
                Error.Validation("Name", "Name cannot be empty."));

        var slug = GenerateSlug(name);

        var organization = new Organization(Guid.NewGuid())
        {
            OwnerUserId = ownerUserId,
            Name = name,
            Slug = slug
        };

        return Result.Success(organization);
    }

    private static string GenerateSlug(string name) =>
        name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("ą", "a").Replace("ć", "c")
            .Replace("ę", "e").Replace("ł", "l")
            .Replace("ń", "n").Replace("ó", "o")
            .Replace("ś", "s").Replace("ź", "z")
            .Replace("ż", "z")
            + "-" + Guid.NewGuid().ToString("N")[..6];
}