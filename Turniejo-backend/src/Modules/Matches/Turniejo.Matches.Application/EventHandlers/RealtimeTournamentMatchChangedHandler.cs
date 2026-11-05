using MediatR;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.EventHandlers
{
    // Wyniki i statusy szły tylko do grup pojedynczych meczów (MatchHub), a portal kibica pokazuje listę
    // wszystkich meczów turnieju — bez tego sygnału lista wymagałaby odświeżania strony.
    internal sealed class RealtimeTournamentMatchChangedHandler(IRealtimeService realtimeService) :
        INotificationHandler<MatchScoreChanged>,
        INotificationHandler<MatchStarted>,
        INotificationHandler<MatchPaused>,
        INotificationHandler<MatchResumed>,
        INotificationHandler<MatchFinished>,
        INotificationHandler<MatchEventAdded>,
        INotificationHandler<MatchEventReverted>,
        INotificationHandler<RefereeAssignedToMatch>,
        INotificationHandler<MatchCancelled>,
        INotificationHandler<MatchScheduleChanged>,
        INotificationHandler<MatchTimeChanged>,
        INotificationHandler<MatchPitchChanged>,
        INotificationHandler<MatchScoreCorrected>,
        INotificationHandler<MatchEventUpdated>
    {
        // Kartki nie zmieniają wyniku — bez tych dwóch strona meczu na portalu nie odświeżyłaby osi czasu.
        public Task Handle(MatchEventAdded notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchEventReverted notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchScoreChanged notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchStarted notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchPaused notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchResumed notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchFinished notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        // Wzięcie, oddanie albo przydzielenie meczu: inni sędziowie muszą od razu zobaczyć, że mecz zniknął
        // z wolnych (albo do nich wrócił). W zdarzeniu idą tylko identyfikatory, bez RefereeId.
        public Task Handle(RefereeAssignedToMatch notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        // Wycofanie drużyny odwołuje jej mecze, a organizator przesuwa termin albo boisko. Bez tych
        // zdarzeń terminarz otwarty u innych pokazywał stary stan aż do odświeżenia strony.
        public Task Handle(MatchCancelled notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchScheduleChanged notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchTimeChanged notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchPitchChanged notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchScoreCorrected notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);

        public Task Handle(MatchEventUpdated notification, CancellationToken cancellationToken) =>
            realtimeService.PublishTournamentMatchChangedAsync(notification.TournamentId, notification.MatchId, cancellationToken);
    }
}
