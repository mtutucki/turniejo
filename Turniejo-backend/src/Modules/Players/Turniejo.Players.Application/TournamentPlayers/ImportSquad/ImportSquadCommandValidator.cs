using FluentValidation;

namespace Turniejo.Players.Application.TournamentPlayers.ImportSquad
{
    public sealed class ImportSquadCommandValidator : AbstractValidator<ImportSquadCommand>
    {
        public ImportSquadCommandValidator()
        {
            RuleFor(x => x.TournamentId)
                .NotEmpty().WithMessage("TournamentId is required.");
            RuleFor(x => x.TournamentTeamId)
                .NotEmpty().WithMessage("TournamentTeamId is required.");
            RuleFor(x => x.SourceTournamentTeamId)
                .NotEmpty().WithMessage("SourceTournamentTeamId is required.")
                .NotEqual(x => x.TournamentTeamId).WithMessage("Source squad must be a different team registration.");
            RuleFor(x => x.Players)
                .NotEmpty().WithMessage("Select at least one player.")
                .Must(players => players.Count <= 100).WithMessage("Too many players selected.")
                .Must(players => players.Select(p => p.PlayerId).Distinct().Count() == players.Count)
                    .WithMessage("Each player can be selected only once.")
                .Must(players =>
                {
                    var numbers = players.Where(p => p.ShirtNumber.HasValue).Select(p => p.ShirtNumber!.Value).ToList();
                    return numbers.Distinct().Count() == numbers.Count;
                }).WithMessage("Shirt numbers must be unique.")
                .When(x => x.Players != null);
            RuleForEach(x => x.Players).ChildRules(player =>
            {
                player.RuleFor(p => p.PlayerId)
                    .NotEmpty().WithMessage("PlayerId is required.");
                player.RuleFor(p => p.ShirtNumber)
                    .GreaterThan(0).When(p => p.ShirtNumber.HasValue).WithMessage("ShirtNumber must be greater than 0.");
            });
        }
    }
}
