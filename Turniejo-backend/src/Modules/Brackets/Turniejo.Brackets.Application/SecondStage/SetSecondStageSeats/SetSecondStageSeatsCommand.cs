using Turniejo.Brackets.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.SecondStage.SetSecondStageSeats
{
    /// <summary>Miejsce w grupie fazy 2: drużyna z miejsca <see cref="SourcePosition"/> w grupie <see cref="SourceGroupId"/>.</summary>
    public sealed record SecondStageSeatInput(Guid GroupId, Guid SourceGroupId, int SourcePosition);

    /// <summary>
    /// Zastępuje cały podział kategorii na grupy drugiej fazy. Kolejność w liście to kolejność miejsc w grupie.
    /// Miejsce z tabeli fazy 1, którego nie ma na liście, kończy dla drużyny turniej po fazie 1.
    /// </summary>
    public sealed record SetSecondStageSeatsCommand(Guid TournamentId, Guid CategoryId, IReadOnlyList<SecondStageSeatInput> Seats)
        : ICommand<int>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.OrganizerOnly;
    }
}
