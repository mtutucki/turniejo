using MediatR;
using Turniejo.Matches.Domain.Enums;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.Application.EventHandlers
{
    // IRealtimeService.PublishMatchStatusChangedAsync istniało, ale nic go nie wywoływało — start, pauza
    // i koniec meczu nie docierały do widzów ani do drugiego urządzenia sędziego bez odświeżenia strony.
    internal sealed class RealtimeMatchStatusChangedHandler(IRealtimeService realtimeService) :
        INotificationHandler<MatchStarted>,
        INotificationHandler<MatchPaused>,
        INotificationHandler<MatchResumed>,
        INotificationHandler<MatchFinished>
    {
        public Task Handle(MatchStarted notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchStatusChangedAsync(notification.MatchId, nameof(MatchStatus.Live), cancellationToken);

        public Task Handle(MatchPaused notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchStatusChangedAsync(notification.MatchId, nameof(MatchStatus.Paused), cancellationToken);

        public Task Handle(MatchResumed notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchStatusChangedAsync(notification.MatchId, nameof(MatchStatus.Live), cancellationToken);

        public Task Handle(MatchFinished notification, CancellationToken cancellationToken) =>
            realtimeService.PublishMatchStatusChangedAsync(notification.MatchId, nameof(MatchStatus.Finished), cancellationToken);
    }
}
