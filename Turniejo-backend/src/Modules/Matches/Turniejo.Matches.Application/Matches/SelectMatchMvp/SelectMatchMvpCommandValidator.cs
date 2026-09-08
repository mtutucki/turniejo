using FluentValidation;

namespace Turniejo.Matches.Application.Matches.SelectMatchMvp
{
    public sealed class SelectMatchMvpCommandValidator : AbstractValidator<SelectMatchMvpCommand>
    {
        public SelectMatchMvpCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.MatchId)
                .NotEmpty().WithMessage("MatchId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.PlayerId)
                .NotEmpty().WithMessage("PlayerId is required.");
        }
    }
}
