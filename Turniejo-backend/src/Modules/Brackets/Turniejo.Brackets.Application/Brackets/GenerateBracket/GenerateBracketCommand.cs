using Turniejo.Brackets.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.Brackets.GenerateBracket
{
    public sealed record GenerateBracketCommand(
        Guid TournamentId, Guid CategoryId, IReadOnlyList<BracketPairingInput> RoundOnePairings)
        : ICommand<GenerateBracketResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.OrganizerOnly;
    }
}
