using NSubstitute;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class RealtimeMatchStatusChangedHandlerTests
    {
        private readonly IRealtimeService _realtimeService = Substitute.For<IRealtimeService>();
        private readonly RealtimeMatchStatusChangedHandler _handler;
        private readonly Guid _matchId = Guid.NewGuid();
        private readonly Guid _tournamentId = Guid.NewGuid();

        public RealtimeMatchStatusChangedHandlerTests()
        {
            _handler = new RealtimeMatchStatusChangedHandler(_realtimeService);
        }

        [Fact]
        public async Task MatchStarted_ShouldPublishLive()
        {
            await _handler.Handle(new MatchStarted(_matchId, _tournamentId, DateTimeOffset.UtcNow, Guid.NewGuid()), CancellationToken.None);

            await _realtimeService.Received(1).PublishMatchStatusChangedAsync(_matchId, "Live", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchPaused_ShouldPublishPaused()
        {
            await _handler.Handle(new MatchPaused(_matchId, _tournamentId, DateTimeOffset.UtcNow, Guid.NewGuid()), CancellationToken.None);

            await _realtimeService.Received(1).PublishMatchStatusChangedAsync(_matchId, "Paused", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchResumed_ShouldPublishLive()
        {
            await _handler.Handle(new MatchResumed(_matchId, _tournamentId, Guid.NewGuid()), CancellationToken.None);

            await _realtimeService.Received(1).PublishMatchStatusChangedAsync(_matchId, "Live", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchFinished_ShouldPublishFinished()
        {
            await _handler.Handle(
                new MatchFinished(_matchId, _tournamentId, null, "Regular", null, DateTimeOffset.UtcNow, Guid.NewGuid(), 1, 1),
                CancellationToken.None);

            await _realtimeService.Received(1).PublishMatchStatusChangedAsync(_matchId, "Finished", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MatchEventReverted_ShouldPublishReverted()
        {
            var handler = new RealtimeMatchEventRevertedHandler(_realtimeService);
            var matchEventId = Guid.NewGuid();

            await handler.Handle(new MatchEventReverted(matchEventId, _matchId, _tournamentId, Guid.NewGuid()), CancellationToken.None);

            await _realtimeService.Received(1).PublishMatchEventRevertedAsync(_matchId, matchEventId, Arg.Any<CancellationToken>());
        }
    }
}
