namespace Turniejo.Teams.Application.DTOs
{
    public sealed record TeamDto(
        Guid Id,
        Guid OrganizationId,
        string Name,
        string? ShortName,
        string? LogoUrl,
        IReadOnlyList<Guid> LabelIds);
}
