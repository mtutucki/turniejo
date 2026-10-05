using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.Services;

public sealed class MatchNotificationRecipientResolverTests
{
    private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
    private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
    private readonly MatchNotificationRecipientResolver _resolver;

    public MatchNotificationRecipientResolverTests()
    {
        _resolver = new MatchNotificationRecipientResolver(_teamModule, _playerModule);
    }

    [Fact]
    public async Task ResolveAsync_ShouldIncludeNotifiableGuardians_OfPlayersOnBothTeams()
    {
        // Audyt: PlayerGuardian.CanReceiveNotifications było przechowywane, ale nigdy nieodczytywane —
        // rodzic ustawiający tę flagę nie dostawał powiadomień (start/koniec/przypomnienie) o meczu dziecka.
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();
        var homePlayerId = Guid.NewGuid();
        var awayPlayerId = Guid.NewGuid();
        var homeGuardianId = Guid.NewGuid();
        var awayGuardianId = Guid.NewGuid();

        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _playerModule.GetTournamentTeamPlayersAsync(homeTeamId, Arg.Any<CancellationToken>())
            .Returns([new TournamentTeamPlayerDto(Guid.NewGuid(), homeTeamId, homePlayerId, "Jan", "Kowalski", 9, "Forward", "Active")]);
        _playerModule.GetTournamentTeamPlayersAsync(awayTeamId, Arg.Any<CancellationToken>())
            .Returns([new TournamentTeamPlayerDto(Guid.NewGuid(), awayTeamId, awayPlayerId, "Ala", "Nowak", 7, "Midfielder", "Active")]);
        _playerModule.GetNotifiableGuardianUserIdsForPlayersAsync(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(homePlayerId) && ids.Contains(awayPlayerId)),
                Arg.Any<CancellationToken>())
            .Returns([homeGuardianId, awayGuardianId]);

        var recipients = await _resolver.ResolveAsync(refereeId: null, homeTeamId, awayTeamId, CancellationToken.None);

        recipients.Should().BeEquivalentTo([homeGuardianId, awayGuardianId]);
    }

    [Fact]
    public async Task ResolveAsync_ShouldNotCallGuardianLookup_WhenNeitherTeamHasPlayers()
    {
        var homeTeamId = Guid.NewGuid();
        var awayTeamId = Guid.NewGuid();

        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _playerModule.GetTournamentTeamPlayersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        var recipients = await _resolver.ResolveAsync(refereeId: null, homeTeamId, awayTeamId, CancellationToken.None);

        recipients.Should().BeEmpty();
        await _playerModule.DidNotReceive().GetNotifiableGuardianUserIdsForPlayersAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }
}
