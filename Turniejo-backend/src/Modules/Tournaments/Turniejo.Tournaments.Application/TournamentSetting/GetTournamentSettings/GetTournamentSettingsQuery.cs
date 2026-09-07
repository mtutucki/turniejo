using Turniejo.SharedKernel.CQRS;
using Turniejo.Tournaments.Application.DTOs;

namespace Turniejo.Tournaments.Application.TournamentSetting.GetTournamentSettings
{
    public sealed record GetTournamentSettingsQuery(Guid TournamentId) : IQuery<TournamentSettingsDto>;
}
