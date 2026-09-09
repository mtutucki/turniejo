using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Stats.GetPublicTopScorers;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Stats
{
    // Zamyka lukę z audytu: rankingi (strzelcy/asysty/MVP) nie miały publicznego, nieuwierzytelnionego
    // wariantu, mimo że TournamentPublicSettings i każdy inny publiczny endpoint (tabela, drabinka,
    // terminarz) go ma. GetPublicTopAssists/GetPublicMvpRanking mają dokładnie tę samą strukturę,
    // więc pokrywają je tylko testy na NotFound/ShowStats — pełną logikę rankingu pokrywa już
    // GetTopScorersQueryHandlerTests dla wariantu prywatnego (identyczna implementacja licząca).
    public sealed class GetPublicTopScorersQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IPlayerModule _playerModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetPublicTopScorersQueryHandler _handler;

        public GetPublicTopScorersQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _playerModule = Substitute.For<IPlayerModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetPublicTopScorersQueryHandler(_dbContext, _playerModule, _tournamentModule);
        }

        private static PublicTournamentDto CreatePublicTournamentDto(Guid tournamentId, bool showStats) =>
            new(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, true, showStats));

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            var result = await _handler.Handle(new GetPublicTopScorersQuery("unknown"), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenShowStatsIsFalse()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showStats: false));

            var result = await _handler.Handle(new GetPublicTopScorersQuery("cup-slug"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
            await _dbContext.DidNotReceive().GetMatchEventsByTournamentIdAndTypesAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnRanking_WhenShowStatsIsTrue()
        {
            var tournamentId = Guid.NewGuid();
            var scorerId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(CreatePublicTournamentDto(tournamentId, showStats: true));

            var goalEvent = MatchEvent.Create(Guid.NewGuid(), Guid.NewGuid(), MatchEventType.Goal, 10, Guid.NewGuid(), Guid.NewGuid(), scorerId).Value;
            _dbContext.GetMatchEventsByTournamentIdAndTypesAsync(
                tournamentId, Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>())
                .Returns([goalEvent]);
            _playerModule.GetPlayersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns([new PlayerDto(scorerId, "Jan", "Kowalski")]);

            var result = await _handler.Handle(new GetPublicTopScorersQuery("cup-slug"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].PlayerId.Should().Be(scorerId);
            result.Value[0].Count.Should().Be(1);
        }
    }
}
