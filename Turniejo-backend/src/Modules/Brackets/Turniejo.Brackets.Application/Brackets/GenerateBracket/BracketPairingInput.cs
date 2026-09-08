using Turniejo.Brackets.Domain.Enums;

namespace Turniejo.Brackets.Application.Brackets.GenerateBracket
{
    public sealed record BracketPairingInput(
        BracketSourceType HomeSourceType, Guid? HomeGroupId, int? HomePosition, Guid? HomeTournamentTeamId,
        BracketSourceType AwaySourceType, Guid? AwayGroupId, int? AwayPosition, Guid? AwayTournamentTeamId);
}
