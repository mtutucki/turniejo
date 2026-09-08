using FluentValidation;

namespace Turniejo.Matches.Application.Matches.CreateBracketMatch
{
    public sealed class CreateBracketMatchCommandValidator : AbstractValidator<CreateBracketMatchCommand>
    {
        public CreateBracketMatchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");

            RuleFor(x => x.HomeTournamentTeamId)
                .NotEmpty().WithMessage("HomeTournamentTeamId is required.");

            RuleFor(x => x.AwayTournamentTeamId)
                .NotEmpty().WithMessage("AwayTournamentTeamId is required.")
                .NotEqual(x => x.HomeTournamentTeamId).WithMessage("A team cannot play against itself.");

            RuleFor(x => x.StageType)
                .IsInEnum().WithMessage("StageType must be a valid enum value.");
        }
    }
}
