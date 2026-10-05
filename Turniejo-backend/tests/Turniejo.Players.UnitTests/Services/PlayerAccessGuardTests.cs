using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.Services
{
    // PlayerAccessGuard nie miał własnego pliku testowego mimo bycia sercem fixa IDOR z początku tej
    // sesji (dane osobowe zawodnika czytelne/edytowalne przez każdego) — był testowany tylko pośrednio,
    // przez pojedyncze scenariusze w handlerach (self, pusty opiekun->Unauthorized). Ścieżki
    // Coach/Organizer (HasStaffAccessAsync) i rozróżnienie CanView vs CanEdit dla opiekuna bez
    // CanEdit nie były nigdzie bezpośrednio sprawdzone.
    public sealed class PlayerAccessGuardTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly IAuthorizationService _authorizationService;
        private readonly PlayerAccessGuard _guard;

        public PlayerAccessGuardTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _guard = new PlayerAccessGuard(_dbContext, _teamModule, _authorizationService);

            _dbContext.GetPlayerGuardiansByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
            _dbContext.GetTournamentPlayersByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        }

        private static Player CreatePlayer(Guid? userId = null) =>
            Player.Create(Guid.NewGuid(), "Jan", "Kowalski", new DateTime(2012, 1, 1), PlayerPosition.Forward, null, userId, Guid.NewGuid()).Value;

        [Fact]
        public async Task CanViewAsync_ShouldAllow_WhenUserIsThePlayerThemselves()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer(userId);

            var result = await _guard.CanViewAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanViewAsync_ShouldAllow_WhenUserIsAnyGuardian_EvenWithoutCanEdit()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, userId, RelationshipType.Parent, canEdit: false, true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([guardian]);

            var result = await _guard.CanViewAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanViewAsync_ShouldAllow_WhenUserIsCoachOfPlayersTeam()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer();
            var tournamentTeamId = Guid.NewGuid();
            var tp = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([tp]);
            _authorizationService.HasAnyRoleAsync(userId, "TournamentTeam", tournamentTeamId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanViewAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanViewAsync_ShouldAllow_WhenUserIsOrganizerOfPlayersTournament()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer();
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var tp = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([tp]);
            _authorizationService.HasAnyRoleAsync(userId, "TournamentTeam", tournamentTeamId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(tournamentTeamId, tournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanViewAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanViewAsync_ShouldDeny_WhenTotalStranger()
        {
            var player = CreatePlayer();

            var result = await _guard.CanViewAsync(player, Guid.NewGuid(), CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanViewAsync_ShouldSkip_WhenTournamentTeamLookupMissesForStaffCheck()
        {
            // Osierocony TournamentPlayer (drużyna już niedostępna) nie powinien wywalić wyjątku,
            // tylko przejść do kolejnego wpisu / ostatecznie odmówić dostępu.
            var player = CreatePlayer();
            var tournamentTeamId = Guid.NewGuid();
            var tp = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([tp]);
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), "TournamentTeam", tournamentTeamId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _teamModule.GetTournamentTeamAsync(tournamentTeamId, Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);

            var result = await _guard.CanViewAsync(player, Guid.NewGuid(), CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanEditAsync_ShouldAllow_WhenUserIsThePlayerThemselves()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer(userId);

            var result = await _guard.CanEditAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanEditAsync_ShouldAllow_WhenGuardianHasCanEdit()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, userId, RelationshipType.Parent, canEdit: true, true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([guardian]);

            var result = await _guard.CanEditAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanEditAsync_ShouldDeny_WhenGuardianLacksCanEdit_AndHasNoStaffAccess()
        {
            // Kluczowe rozróżnienie od CanViewAsync: sam status opiekuna NIE wystarcza do edycji —
            // trzeba mieć jawnie ustawione CanEdit=true, inaczej to widz-only.
            var userId = Guid.NewGuid();
            var player = CreatePlayer();
            var guardian = PlayerGuardian.Create(Guid.NewGuid(), player.Id, userId, RelationshipType.Parent, canEdit: false, true, Guid.NewGuid()).Value;
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([guardian]);

            var result = await _guard.CanEditAsync(player, userId, CancellationToken.None);

            result.Should().BeFalse();
        }

        [Fact]
        public async Task CanEditAsync_ShouldAllow_WhenUserIsCoachOfPlayersTeam()
        {
            var userId = Guid.NewGuid();
            var player = CreatePlayer();
            var tournamentTeamId = Guid.NewGuid();
            var tp = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByPlayerIdAsync(player.Id, Arg.Any<CancellationToken>()).Returns([tp]);
            _authorizationService.HasAnyRoleAsync(userId, "TournamentTeam", tournamentTeamId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var result = await _guard.CanEditAsync(player, userId, CancellationToken.None);

            result.Should().BeTrue();
        }

        [Fact]
        public async Task CanEditAsync_ShouldDeny_WhenTotalStranger()
        {
            var player = CreatePlayer();

            var result = await _guard.CanEditAsync(player, Guid.NewGuid(), CancellationToken.None);

            result.Should().BeFalse();
        }
    }
}
