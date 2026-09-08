using FluentValidation;

namespace Turniejo.Notifications.Application.Notifications.CreateOrganizerAnnouncement
{
    public sealed class CreateOrganizerAnnouncementCommandValidator : AbstractValidator<CreateOrganizerAnnouncementCommand>
    {
        public CreateOrganizerAnnouncementCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");
            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("Message is required.")
                .MaximumLength(2000).WithMessage("Message cannot exceed 2000 characters.");
        }
    }
}
