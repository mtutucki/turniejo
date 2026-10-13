using Turniejo.SharedKernel.Helpers;
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

        var slug = SlugGenerator.Generate(name);

        var organization = new Organization(Guid.NewGuid())
        {
            OwnerUserId = ownerUserId,
            Name = name,
            Slug = slug
        };

        return Result.Success(organization);
    }

    /// <summary>Zmiana nazwy. Slug zostaje — jest stałym identyfikatorem, nie etykietą.</summary>
    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Name cannot be empty."));

        Name = name.Trim();
        SetUpdatedAt();
        return Result.Success();
    }

}