using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Users.Application.Users.GetTournamentMembers;

// Osoby z rolą w turnieju (organizatorzy + ci, którzy dołączyli kodem). Służy organizatorowi do
// przypisania trenera do drużyny bez wyszukiwania dowolnych kont po emailu (brak enumeracji userów).
public sealed record GetTournamentMembersQuery(Guid TournamentId)
    : IQuery<IReadOnlyList<TournamentMemberDto>>, IAuthorizedRequest
{
    public string ScopeType => nameof(RoleScopeType.Tournament);
    public Guid ScopeId => TournamentId;
    public IReadOnlyCollection<string> AllowedRoles => [nameof(UserRole.Organizer)];
}

public sealed record TournamentMemberDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);
