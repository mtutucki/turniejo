using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;

namespace Turniejo.Matches.UnitTests.Services
{
    public sealed class RealtimeAccessGuardTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly RealtimeAccessGuard _guard;

        public RealtimeAccessGuardTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _guard = new RealtimeAccessGuard(_dbContext, _tournamentModule, _authorizationService);
        }

        private static Match CreateMatch(Guid tournamentId) =>
            Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, StageType.Group).Value;

        // --- CanJoinTournamentGroupAsync ---

        [Fact]
        public async Task CanJoinTournamentGroup_ShouldAllowAnonymous_WhenTournamentIsPublic()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, true, true));

            var result = await _guard.CanJoinTournamentGroupAsync(tournamentId, userId: null, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanJoinTournamentGroup_ShouldDenyAnonymous_WhenTournamentIsPrivate()
        {
            // Zamyka lukę z audytu: niezalogowany klient nie może dołączyć do grupy prywatnego turnieju.
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));

            var result = await _guard.CanJoinTournamentGroupAsync(tournamentId, userId: null, CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanJoinTournamentGroup_ShouldAllowMember_WhenTournamentIsPrivate()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanJoinTournamentGroupAsync(tournamentId, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanJoinTournamentGroup_ShouldDenyOutsider_WhenTournamentIsPrivate()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);

            var result = await _guard.CanJoinTournamentGroupAsync(tournamentId, userId, CancellationToken.None);

            result.Should().BeFalse();
        }

        // --- CanJoinMatchGroupAsync ---

        [Fact]
        public async Task CanJoinMatchGroup_ShouldDeny_WhenMatchDoesNotExist()
        {
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            var result = await _guard.CanJoinMatchGroupAsync(matchId, userId: null, CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanJoinMatchGroup_ShouldAllowAnonymous_WhenTournamentPublicAndShowLiveMatches()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, true, true));

            var result = await _guard.CanJoinMatchGroupAsync(match.Id, userId: null, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanJoinMatchGroup_ShouldDenyAnonymous_WhenShowLiveMatchesIsFalse()
        {
            var tournamentId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, true, true, true, true, true, true, false, true));

            var result = await _guard.CanJoinMatchGroupAsync(match.Id, userId: null, CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanJoinMatchGroup_ShouldDenyAnonymous_WhenTournamentIsPrivate()
        {
            // Kluczowy scenariusz z audytu: prywatny turniej nie wycieka przez grupę SignalR
            // nawet gdy klient w ogóle nie jest zalogowany.
            var tournamentId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));

            var result = await _guard.CanJoinMatchGroupAsync(match.Id, userId: null, CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanJoinMatchGroup_ShouldAllowTournamentMember_WhenTournamentIsPrivate()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanJoinMatchGroupAsync(match.Id, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanJoinMatchGroup_ShouldAllowRefereeAssignedToMatch_WhenNotTournamentMember()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Match), match.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanJoinMatchGroupAsync(match.Id, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanJoinMatchGroup_ShouldDenyOutsider_WhenTournamentIsPrivate()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var match = CreateMatch(tournamentId);
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _tournamentModule.GetTournamentPublicSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentPublicSettingsDto(tournamentId, false, true, true, true, true, true, true, true));
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Tournament), tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _authorizationService.HasAnyRoleAsync(userId, nameof(RoleScopeType.Match), match.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);

            var result = await _guard.CanJoinMatchGroupAsync(match.Id, userId, CancellationToken.None);

            result.Should().BeFalse();
        }
    }
}
