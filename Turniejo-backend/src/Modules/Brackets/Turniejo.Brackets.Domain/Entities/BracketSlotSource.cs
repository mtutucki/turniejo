using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.Domain.Entities
{
    public sealed record BracketSlotSource(
        BracketSourceType SourceType, Guid? GroupId, int? Position, Guid? TournamentTeamId);
}
