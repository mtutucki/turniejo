namespace Turniejo.SharedKernel.Contracts;

public interface ITeamModule
{
    Task<TeamDto?> GetTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(IReadOnlyCollection<Guid> teamIds, CancellationToken cancellationToken = default);
    Task<TournamentTeamDto?> GetTournamentTeamAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TournamentTeamDto>> GetTournamentTeamsByTournamentIdAsync(Guid tournamentId, CancellationToken cancellationToken = default);
    // Wszystkie zgłoszenia tej samej drużyny (we wszystkich turniejach) — moduł Players bierze z nich
    // poprzednie składy do skopiowania; TournamentPlayer zna tylko TournamentTeamId.
    Task<IReadOnlyList<TournamentTeamDto>> GetTournamentTeamsByTeamIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetTeamStaffUserIdsAsync(Guid tournamentTeamId, CancellationToken cancellationToken = default);

    // Cross-module istnienie-check dla blokad kasowania kategorii turnieju — TournamentTeam.CategoryId
    // nie ma FK. Blokują tylko oczekujące i zaakceptowane; odrzucone/wycofane znikają razem z kategorią.
    Task<bool> AnyActiveTeamsInCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);
    /// <summary>Odrzucone i wycofane zgłoszenia kategorii (lista drużyn ich nie pokazuje).</summary>
    Task<IReadOnlyList<Guid>> GetInactiveTournamentTeamIdsByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
    /// <summary>Usuwa zgłoszenia od razu w bazie (sztab znika kaskadą FK).</summary>
    Task RemoveTournamentTeamsAsync(IReadOnlyCollection<Guid> tournamentTeamIds, CancellationToken cancellationToken = default);
    // Jak wyżej, dla TournamentTeam.GroupId — blokada kasowania grupy turnieju.
    Task<bool> AnyTeamsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default);
    /// <summary>Drużyny kategorii bez grupy (poza odrzuconymi/wycofanymi) trafiają do wskazanej grupy.</summary>
    Task<int> AssignUngroupedTeamsToGroupAsync(Guid tournamentId, Guid categoryId, Guid groupId, CancellationToken cancellationToken = default);

    // Usunięcie organizacji: zgłoszenia jej drużyn (potrzebne modułowi Players do usunięcia składów),
    // a potem cała baza drużyn ze zgłoszeniami i sztabem.
    Task<IReadOnlyList<Guid>> GetTournamentTeamIdsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task RemoveTeamsByOrganizationIdAsync(Guid organizationId, CancellationToken cancellationToken = default);
}

public record TeamDto(Guid Id, string Name, string? ShortName);
public record TournamentTeamDto(Guid Id, Guid TournamentId, Guid TeamId, Guid CategoryId, Guid? GroupId, string RegistrationStatus);
