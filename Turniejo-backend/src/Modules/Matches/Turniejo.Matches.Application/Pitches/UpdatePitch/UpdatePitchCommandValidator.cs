using FluentValidation;

namespace Turniejo.Matches.Application.Pitches.UpdatePitch
{
    public sealed class UpdatePitchCommandValidator : AbstractValidator<UpdatePitchCommand>
    {
        public UpdatePitchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.PitchId)
                .NotEmpty().WithMessage("PitchId is required.");
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.");
        }
    }
}
