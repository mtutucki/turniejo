using Turniejo.Brackets.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.Brackets.ResetBracket
{
    // Drabinka jest generowana raz (GenerateBracket odmawia, gdy już istnieje), a węzły mają zamrożone
    // drużyny — po wycofaniu drużyny z pary nie było żadnej drogi naprawy: para zostawała martwa, a slot
    // w kolejnej rundzie nigdy się nie wypełniał. Reset usuwa drabinkę razem z jej meczami, żeby
    // organizator ułożył pary od nowa.
    public sealed record ResetBracketCommand(Guid TournamentId, Guid CategoryId) : ICommand<ResetBracketResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.OrganizerOnly;
    }

    public sealed record ResetBracketResponse(int MatchesRemoved);
}
