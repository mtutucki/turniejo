using Turniejo.Brackets.Application.Authorization;
using Turniejo.SharedKernel.CQRS;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Brackets.Application.SecondStage.SetSecondStageRanges
{
    /// <summary>Grupa fazy 2 dostaje miejsca od <see cref="FromPosition"/> do <see cref="ToPosition"/> z każdej grupy fazy 1.</summary>
    public sealed record SecondStageRangeInput(Guid GroupId, int FromPosition, int ToPosition);

    /// <summary>
    /// Zastępuje podział kategorii zakresami miejsc. Pojedynczo ustawione miejsca tej kategorii znikają,
    /// a konkretne miejsca powstaną z zakresów przy generowaniu terminarza.
    /// </summary>
    public sealed record SetSecondStageRangesCommand(Guid TournamentId, Guid CategoryId, IReadOnlyList<SecondStageRangeInput> Ranges)
        : ICommand<int>, IAuthorizedRequest
    {
        public string ScopeType => nameof(RoleScopeType.Tournament);
        public Guid ScopeId => TournamentId;
        public IReadOnlyCollection<string> AllowedRoles => BracketRoles.OrganizerOnly;
    }
}
