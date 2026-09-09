using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Stats.GetPublicTopAssists;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Stats
{
    public sealed class GetPublicTopAssistsQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly GetPublicTopAssistsQueryHandler _handler;

        public GetPublicTopAssistsQueryHandlerTests()
        {
            _handler = new GetPublicTopAssistsQueryHandler(_dbContext, _playerModule, _tournamentModule);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFoundOrNotPublic()
        {
            _tournamentModule.GetPublicTournamentBySlugAsync("unknown", Arg.Any<CancellationToken>())
                .Returns((PublicTournamentDto?)null);

            var result = await _handler.Handle(new GetPublicTopAssistsQuery("unknown"), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnEmpty_WhenShowStatsIsFalse()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetPublicTournamentBySlugAsync("cup-slug", Arg.Any<CancellationToken>())
                .Returns(new PublicTournamentDto(tournamentId, "Cup", "cup-slug", "InProgress", "Group",
                    DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), null,
                    new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, true, false)));

            var result = await _handler.Handle(new GetPublicTopAssistsQuery("cup-slug"), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
            await _dbContext.DidNotReceive().GetMatchEventsByTournamentIdAndTypesAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<MatchEventType>>(), Arg.Any<CancellationToken>());
        }
    }
}
