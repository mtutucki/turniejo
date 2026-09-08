using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.UnitTests.Services
{
    public sealed class MatchAccessGuardTests
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IPlayerModule _playerModule;
        private readonly MatchAccessGuard _guard;

        public MatchAccessGuardTests()
        {
            _authorizationService = Substitute.For<IAuthorizationService>();
            _playerModule = Substitute.For<IPlayerModule>();
            _guard = new MatchAccessGuard(_authorizationService, _playerModule);
        }

        private static Match CreateMatch(Guid tournamentId) =>
            Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, StageType.Group).Value;

        [Fact]
        public async Task CanViewAsync_ShouldAllow_WhenUserIsTournamentMember()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanViewAsync(match, userId, CancellationToken.None);

            result.Should().BeTrue();
            await _playerModule.DidNotReceive().IsGuardianOfAnyPlayerOnTeamsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CanViewAsync_ShouldAllow_WhenUserIsGuardianOfAPlayerOnEitherTeam()
        {
            // Domyka lukę z audytu: rodzic zawodnika grającego w meczu widzi jego lokalizację,
            // mimo że nie ma żadnej roli w turnieju.
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _playerModule.IsGuardianOfAnyPlayerOnTeamsAsync(
                userId,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(match.HomeTournamentTeamId) && ids.Contains(match.AwayTournamentTeamId)),
                Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanViewAsync(match, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanViewAsync_ShouldDeny_WhenUserIsNeitherMemberNorGuardian()
        {
            // Zamyka IDOR z audytu: dowolny zalogowany user widział lokalizację dowolnego meczu.
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _playerModule.IsGuardianOfAnyPlayerOnTeamsAsync(userId, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(false);

            var result = await _guard.CanViewAsync(match, userId, CancellationToken.None);

            result.Should().BeFalse();
        }
    }
}
