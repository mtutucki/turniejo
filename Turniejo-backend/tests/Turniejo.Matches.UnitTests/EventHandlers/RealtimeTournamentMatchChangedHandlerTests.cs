using NSubstitute;
using Turniejo.Matches.Application.EventHandlers;
using Turniejo.Matches.Domain.Events;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.EventHandlers
{
    public sealed class RealtimeTournamentMatchChangedHandlerTests
    {
        private readonly IRealtimeService _realtimeService = Substitute.For<IRealtimeService>();
        private readonly RealtimeTournamentMatchChangedHandler _handler;
        private readonly Guid _matchId = Guid.NewGuid();
        private readonly Guid _tournamentId = Guid.NewGuid();

        public RealtimeTournamentMatchChangedHandlerTests()
        {
            _handler = new RealtimeTournamentMatchChangedHandler(_realtimeService);
        }

        private Task ShouldHavePublished() =>
            _realtimeService.Received(1).PublishTournamentMatchChangedAsync(_tournamentId, _matchId, Arg.Any<CancellationToken>());

        [Fact]
        public async Task ScoreChanged_ShouldNotifyTournamentGroup()
        {
            await _handler.Handle(new MatchScoreChanged(_matchId, _tournamentId, null, 1, 0), CancellationToken.None);
            await ShouldHavePublished();
        }

        [Fact]
        public async Task Started_ShouldNotifyTournamentGroup()
        {
            await _handler.Handle(new MatchStarted(_matchId, _tournamentId, DateTimeOffset.UtcNow, Guid.NewGuid()), CancellationToken.None);
            await ShouldHavePublished();
        }

        [Fact]
        public async Task Finished_ShouldNotifyTournamentGroup()
        {
            await _handler.Handle(
                new MatchFinished(_matchId, _tournamentId, null, "Regular", null, DateTimeOffset.UtcNow, Guid.NewGuid(), 2, 1),
                CancellationToken.None);
            await ShouldHavePublished();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RefereeAssignedOrReleased_ShouldNotifyTournamentGroup(bool assigned)
        {
            var refereeId = Guid.NewGuid();
            await _handler.Handle(
                new RefereeAssignedToMatch(_matchId, _tournamentId, assigned ? refereeId : null, refereeId),
                CancellationToken.None);
            await ShouldHavePublished();
        }
    }
}
