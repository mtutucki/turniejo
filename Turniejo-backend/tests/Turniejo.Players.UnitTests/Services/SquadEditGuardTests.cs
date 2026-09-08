using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Services;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.Services
{
    // SquadEditGuard nie miał własnego pliku testowego mimo bycia sercem osobnego fixa z tej sesji
    // ("Trener omija blokadę edycji składu po starcie turnieju") — był testowany tylko pośrednio,
    // przez pojedyncze scenariusze wplecione w testy handlerów, które go wołają.
    public sealed class SquadEditGuardTests
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly ITournamentModule _tournamentModule;
        private readonly SquadEditGuard _guard;

        public SquadEditGuardTests()
        {
            _authorizationService = Substitute.For<IAuthorizationService>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _guard = new SquadEditGuard(_authorizationService, _tournamentModule);
        }

        private static TournamentSettingsSummaryDto Settings(Guid tournamentId, bool canCoachEditAfterStart) =>
            new(tournamentId, 30, 10, 60, 3, 1, 0, false, canCoachEditAfterStart, 3, 0);

        [Fact]
        public async Task EnsureCanEditSquadAsync_ShouldAllow_WhenUserIsOrganizer_RegardlessOfTournamentStatus()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var act = () => _guard.EnsureCanEditSquadAsync(tournamentId, userId, CancellationToken.None);

            await act.Should().NotThrowAsync();
            await _tournamentModule.DidNotReceive().GetTournamentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task EnsureCanEditSquadAsync_ShouldAllow_WhenTournamentHasNotStarted()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "RegistrationOpen", null, Guid.NewGuid(), 8));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(Settings(tournamentId, canCoachEditAfterStart: false));

            var act = () => _guard.EnsureCanEditSquadAsync(tournamentId, userId, CancellationToken.None);

            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task EnsureCanEditSquadAsync_ShouldAllow_WhenStartedButCanCoachEditSquadAfterStartIsTrue()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 8));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(Settings(tournamentId, canCoachEditAfterStart: true));

            var act = () => _guard.EnsureCanEditSquadAsync(tournamentId, userId, CancellationToken.None);

            await act.Should().NotThrowAsync();
        }

        [Theory]
        [InlineData("InProgress")]
        [InlineData("Finished")]
        public async Task EnsureCanEditSquadAsync_ShouldThrow_WhenStartedAndCoachCannotEditAfterStart(string status)
        {
            // To jest dokładnie luka z audytu, którą ten guard zamyka: trener nie może już edytować
            // składu po starcie turnieju, jeśli organizator tego nie zezwolił.
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", status, "Group", Guid.NewGuid(), 8));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(Settings(tournamentId, canCoachEditAfterStart: false));

            var act = () => _guard.EnsureCanEditSquadAsync(tournamentId, userId, CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        [Fact]
        public async Task EnsureCanEditSquadAsync_ShouldFailClosed_WhenTournamentNotFound()
        {
            // Bez tego nieistniejący/sfałszowany TournamentId omijał blokadę (fail-open) zamiast
            // dostać jednoznaczną odmowę.
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns((TournamentDto?)null);
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(Settings(tournamentId, canCoachEditAfterStart: true));

            var act = () => _guard.EnsureCanEditSquadAsync(tournamentId, userId, CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        [Fact]
        public async Task EnsureCanEditSquadAsync_ShouldFailClosed_WhenSettingsNotFound()
        {
            var tournamentId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            _authorizationService.HasAnyRoleAsync(userId, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "RegistrationOpen", null, Guid.NewGuid(), 8));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns((TournamentSettingsSummaryDto?)null);

            var act = () => _guard.EnsureCanEditSquadAsync(tournamentId, userId, CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
