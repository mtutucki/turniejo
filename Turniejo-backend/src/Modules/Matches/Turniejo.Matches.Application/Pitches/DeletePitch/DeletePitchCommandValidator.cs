using FluentValidation;

namespace Turniejo.Matches.Application.Pitches.DeletePitch
{
    public sealed class DeletePitchCommandValidator : AbstractValidator<DeletePitchCommand>
    {
        public DeletePitchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");

            RuleFor(x => x.PitchId)
                .NotEmpty().WithMessage("PitchId is required.");
        }
    }
}
