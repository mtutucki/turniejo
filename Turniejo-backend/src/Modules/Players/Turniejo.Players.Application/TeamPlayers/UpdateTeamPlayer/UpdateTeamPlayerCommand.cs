using FluentValidation;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Players.Application.TeamPlayers.UpdateTeamPlayer
{
    // Profil zawodnika i jego domyślny numer w kadrze. Handler sprawdza TeamRosterAccessGuard.
    public sealed record UpdateTeamPlayerCommand(
        Guid TeamId,
        Guid PlayerId,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        PlayerPosition DefaultPosition,
        int? ShirtNumber) : ICommand<UpdateTeamPlayerResponse>, IAuthenticatedOnlyRequest;

    public sealed record UpdateTeamPlayerResponse(Guid PlayerId);

    public sealed class UpdateTeamPlayerCommandValidator : AbstractValidator<UpdateTeamPlayerCommand>
    {
        public UpdateTeamPlayerCommandValidator()
        {
            RuleFor(x => x.TeamId)
                .NotEmpty().WithMessage("TeamId is required.");
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
            RuleFor(x => x.FirstName)
                .NotEmpty().WithMessage("FirstName is required.")
                .MaximumLength(100).WithMessage("FirstName must not exceed 100 characters.");
            RuleFor(x => x.LastName)
                .NotEmpty().WithMessage("LastName is required.")
                .MaximumLength(100).WithMessage("LastName must not exceed 100 characters.");
            RuleFor(x => x.BirthDate)
                .LessThanOrEqualTo(DateTime.UtcNow.Date).WithMessage("BirthDate cannot be in the future.");
            RuleFor(x => x.ShirtNumber)
                .GreaterThan(0).When(x => x.ShirtNumber.HasValue).WithMessage("ShirtNumber must be greater than 0.");
        }
    }
}
