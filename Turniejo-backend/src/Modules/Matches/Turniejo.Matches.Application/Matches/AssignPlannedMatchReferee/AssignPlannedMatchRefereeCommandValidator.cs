using FluentValidation;

namespace Turniejo.Matches.Application.Matches.AssignPlannedMatchReferee
{
    public sealed class AssignPlannedMatchRefereeCommandValidator : AbstractValidator<AssignPlannedMatchRefereeCommand>
    {
        public AssignPlannedMatchRefereeCommandValidator()
        {
            RuleFor(x => x.TournamentId).NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.NodeId).NotEmpty().WithMessage("NodeId is required.");
        }
    }
}
