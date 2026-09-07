using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;
using Turniejo.Teams.Application.Authorization;
using Turniejo.Teams.Domain.Enums;

namespace Turniejo.Teams.Application.TournamentTeams.ChangeTournamentTeamPaymentStatus
{
    public sealed record ChangeTournamentTeamPaymentStatusCommand(
        Guid TournamentId,
        Guid TournamentTeamId,
        PaymentStatus NewStatus) : ICommand<ChangeTournamentTeamPaymentStatusResponse>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => TeamRoles.OrganizerOnly;
    }
}
