namespace Turniejo.SharedKernel.Contracts;

public interface IOrganizationModule
{
    /// <summary>Nazwy organizacji po Id (lista turniejów publicznych pokazuje organizatora). Brakujące Id pomija.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetOrganizationNamesAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Usuwa organizację razem z jej danymi: zawodników (i ich opiekunów), bazę drużyn i zgłoszenia,
    /// a turnieje ukrywa (miękkie usunięcie — mecze, tabele i drabinki przestają być gdziekolwiek widoczne).
    /// Idempotentne. Zwraca zasoby, do których role trzeba jeszcze odebrać (moduł Users).
    /// </summary>
    Task<DeletedOrganizationDto> DeleteOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

public record DeletedOrganizationDto(
    IReadOnlyList<Guid> TournamentIds,
    IReadOnlyList<Guid> TournamentTeamIds,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyList<Guid> MatchIds);
