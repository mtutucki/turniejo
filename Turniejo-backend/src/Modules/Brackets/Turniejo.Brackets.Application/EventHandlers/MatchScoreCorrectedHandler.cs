using MediatR;
using Turniejo.Brackets.Application.Brackets.AdvanceBracketWinner;
using Turniejo.Matches.Domain.Events;

namespace Turniejo.Brackets.Application.EventHandlers
{
    // Domyka lukę z audytu: korekta wyniku zakończonego meczu (w oknie edycji) mogła zmienić
    // zwycięzcę, ale drabinka słuchała tylko MatchFinished — pozostawał w niej awansowany, już
    // nieaktualny zespół. AdvanceBracketWinnerCommand jest bezpieczny do ponownego wywołania: gdy
    // zwycięzca się nie zmienił, to no-op; gdy się zmienił, a węzeł docelowy nie ma jeszcze
    // przypisanego meczu, poprawia slot; gdy węzeł docelowy już ma mecz, BracketAdvancer odmawia
    // cichej podmianki (patrz komentarz w BracketAdvancer.AdvanceIntoAsync).
    internal sealed class MatchScoreCorrectedHandler(ISender sender) : INotificationHandler<MatchScoreCorrected>
    {
        public async Task Handle(MatchScoreCorrected notification, CancellationToken cancellationToken)
        {
            if (!notification.WinnerTournamentTeamId.HasValue)
                return;

            await sender.Send(new AdvanceBracketWinnerCommand(notification.MatchId, notification.WinnerTournamentTeamId), cancellationToken);
        }
    }
}
