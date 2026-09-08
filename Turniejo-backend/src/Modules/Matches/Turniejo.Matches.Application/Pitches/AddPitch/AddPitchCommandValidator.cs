using FluentValidation;

namespace Turniejo.Matches.Application.Pitches.AddPitch
{
    public sealed class AddPitchCommandValidator : AbstractValidator<AddPitchCommand>
    {
        public AddPitchCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        }
    }
}
