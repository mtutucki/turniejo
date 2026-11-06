using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.Services;
using Turniejo.Players.Application.TeamPlayers.CreateTeamPlayer;
using Turniejo.Players.Application.TeamPlayers.GetTeamRoster;
using Turniejo.Players.Application.TeamPlayers.RemoveTeamPlayer;
using Turniejo.Players.Application.TeamPlayers.RestoreTeamPlayer;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Enums;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Players.UnitTests.TeamPlayers
{
    public sealed class TeamRosterTests
    {
        private readonly IPlayersDbContext _dbContext = Substitute.For<IPlayersDbContext>();
        private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly IMatchModule _matchModule = Substitute.For<IMatchModule>();
        private readonly IAuthorizationService _authorization = Substitute.For<IAuthorizationService>();
        private readonly ICurrentUserContext _currentUser = Substitute.For<ICurrentUserContext>();

        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _organizationId = Guid.NewGuid();
        private readonly Guid _teamId = Guid.NewGuid();

        private readonly List<TeamPlayer> _members = [];
        private readonly List<Player> _profiles = [];
        private readonly List<TournamentTeamDto> _registrations = [];
        private readonly List<TournamentPlayer> _allSquadEntries = [];
        private readonly HashSet<(string Scope, Guid Id, string Role)> _roles = [];

        public TeamRosterTests()
        {
            _currentUser.UserId.Returns(_userId);
            _teamModule.GetTeamOrganizationIdAsync(_teamId, Arg.Any<CancellationToken>()).Returns(_organizationId);
            _teamModule.GetTeamAsync(_teamId, Arg.Any<CancellationToken>()).Returns(new TeamDto(_teamId, "Orlik", null));
            _teamModule.GetTournamentTeamsByTeamIdAsync(_teamId, Arg.Any<CancellationToken>()).Returns(_registrations);

            _authorization.HasAnyRoleAsync(_userId, Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(call => call.ArgAt<IReadOnlyCollection<string>>(3)
                    .Any(role => _roles.Contains((call.ArgAt<string>(1), call.ArgAt<Guid>(2), role))));

            _dbContext.GetTeamPlayersByTeamIdAsync(_teamId, Arg.Any<CancellationToken>())
                .Returns(_ => _members.Where(m => m.TeamId == _teamId).ToList());
            _dbContext.GetTeamPlayerAsync(_teamId, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => _members.FirstOrDefault(m => m.TeamId == _teamId && m.PlayerId == call.ArgAt<Guid>(1)));
            _dbContext.GetTeamPlayersByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => _members.Where(m => m.PlayerId == call.ArgAt<Guid>(0)).ToList());
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(call => _profiles.Where(p => call.Arg<IReadOnlyCollection<Guid>>().Contains(p.Id)).ToList());
            _dbContext.GetPlayerByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => _profiles.FirstOrDefault(p => p.Id == call.ArgAt<Guid>(0)));
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => _allSquadEntries.Where(tp => tp.TournamentTeamId == call.ArgAt<Guid>(0)).ToList());
            _dbContext.GetTournamentPlayersByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => _allSquadEntries.Where(tp => tp.PlayerId == call.ArgAt<Guid>(0)).ToList());
            _dbContext.GetTournamentPlayerByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => _allSquadEntries.FirstOrDefault(tp => tp.Id == call.ArgAt<Guid>(0)));
            _dbContext.GetPlayerGuardiansByPlayerIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<PlayerGuardian>());
            _matchModule.GetMatchEventsByTournamentTeamIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<TeamMatchEventDto>());
        }

        private void GrantOrganizer() => _roles.Add((nameof(RoleScopeType.Organization), _organizationId, nameof(UserRole.Organizer)));

        private Player Member(string firstName, string lastName, int? shirtNumber, TeamPlayerStatus status = TeamPlayerStatus.Active)
        {
            var player = Player.Create(Guid.NewGuid(), firstName, lastName, new DateTime(2014, 3, 1), PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _profiles.Add(player);
            var member = TeamPlayer.Create(Guid.NewGuid(), _teamId, player.Id, shirtNumber).Value;
            if (status == TeamPlayerStatus.Left)
                member.Leave();
            _members.Add(member);
            return player;
        }

        private (Guid TournamentTeamId, Guid TournamentId) Registration(string tournamentName, string status, bool canCoachEditAfterStart = true)
        {
            var tournamentTeamId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            _registrations.Add(new TournamentTeamDto(tournamentTeamId, tournamentId, _teamId, Guid.NewGuid(), null, "Accepted"));
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, tournamentName, status, null, _organizationId, 16, DateTimeOffset.UtcNow));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 20, 5, 10, 3, 1, 0, false, canCoachEditAfterStart, 3, 0));
            return (tournamentTeamId, tournamentId);
        }

        private TournamentPlayer InSquad(Guid tournamentTeamId, Player player)
        {
            var entry = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, null, null, Guid.NewGuid()).Value;
            entry.ClearDomainEvents();
            _allSquadEntries.Add(entry);
            return entry;
        }

        private TeamRosterAccessGuard AccessGuard() => new(_teamModule, _authorization);

        private TeamSquadsReader SquadsReader() =>
            new(_dbContext, _teamModule, _tournamentModule, new SquadEditGuard(_authorization, _tournamentModule), _authorization);

        private RemoveTeamPlayerCommandHandler RemoveHandler() =>
            new(_dbContext, AccessGuard(), SquadsReader(), new SquadRemoval(_dbContext, _matchModule), _currentUser);

        // --- Dostęp ---------------------------------------------------------------------------------

        [Fact]
        public async Task AccessGuard_ShouldAllowEdit_ForCoachOfAnyRegistration()
        {
            var (tournamentTeamId, _) = Registration("Wiosna", "Finished");
            _roles.Add((nameof(RoleScopeType.TournamentTeam), tournamentTeamId, nameof(UserRole.Coach)));

            (await AccessGuard().GetAccessAsync(_teamId, _userId)).Should().Be(TeamRosterAccess.Edit);
        }

        [Fact]
        public async Task AccessGuard_ShouldAllowOnlyView_ForAssistantCoach()
        {
            var (tournamentTeamId, _) = Registration("Wiosna", "Finished");
            _roles.Add((nameof(RoleScopeType.TournamentTeam), tournamentTeamId, nameof(UserRole.AssistantCoach)));

            (await AccessGuard().GetAccessAsync(_teamId, _userId)).Should().Be(TeamRosterAccess.View);
        }

        [Fact]
        public async Task GetRoster_ShouldThrow_ForUserWithoutAccess()
        {
            var handler = new GetTeamRosterQueryHandler(_dbContext, _teamModule, AccessGuard(), SquadsReader(), _currentUser);

            var act = () => handler.Handle(new GetTeamRosterQuery(_teamId), CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        // --- Lista ----------------------------------------------------------------------------------

        [Fact]
        public async Task GetRoster_ShouldSplitCurrentAndPastTournaments()
        {
            GrantOrganizer();
            var player = Member("Jan", "Kowalski", 9);
            var past = Registration("Jesień", "Finished");
            var upcoming = Registration("Wiosna", "RegistrationOpen");
            InSquad(past.TournamentTeamId, player);
            InSquad(upcoming.TournamentTeamId, player);
            var handler = new GetTeamRosterQueryHandler(_dbContext, _teamModule, AccessGuard(), SquadsReader(), _currentUser);

            var result = await handler.Handle(new GetTeamRosterQuery(_teamId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.CanEdit.Should().BeTrue();
            var dto = result.Value.Players.Single();
            dto.CurrentSquads.Should().ContainSingle(s => s.TournamentName == "Wiosna");
            dto.PastTournamentsCount.Should().Be(1);
        }

        // --- Dodawanie ------------------------------------------------------------------------------

        [Fact]
        public async Task Create_ShouldRejectTakenShirtNumber()
        {
            GrantOrganizer();
            Member("Jan", "Kowalski", 9);
            var handler = new CreateTeamPlayerCommandHandler(_dbContext, AccessGuard(), _currentUser);

            var result = await handler.Handle(
                new CreateTeamPlayerCommand(_teamId, "Adam", "Nowak", new DateTime(2014, 1, 1), PlayerPosition.Defender, 9),
                CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Be("Shirt number is already taken in this team");
        }

        [Fact]
        public async Task Create_ShouldAllowNumberOfPlayerWhoLeft()
        {
            GrantOrganizer();
            Member("Jan", "Kowalski", 9, TeamPlayerStatus.Left);
            var handler = new CreateTeamPlayerCommandHandler(_dbContext, AccessGuard(), _currentUser);

            var result = await handler.Handle(
                new CreateTeamPlayerCommand(_teamId, "Adam", "Nowak", new DateTime(2014, 1, 1), PlayerPosition.Defender, 9),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddTeamPlayerAsync(Arg.Is<TeamPlayer>(m => m.ShirtNumber == 9), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Create_ShouldFlagDuplicate_UnlessConfirmed()
        {
            GrantOrganizer();
            Member("Jan", "Kowalski", null);
            var handler = new CreateTeamPlayerCommandHandler(_dbContext, AccessGuard(), _currentUser);
            var command = new CreateTeamPlayerCommand(_teamId, " jan ", "KOWALSKI", new DateTime(2014, 3, 1), PlayerPosition.Forward, null);

            (await handler.Handle(command, CancellationToken.None)).IsFailure.Should().BeTrue();
            (await handler.Handle(command with { AllowDuplicate = true }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        }

        // --- Usuwanie -------------------------------------------------------------------------------

        [Fact]
        public async Task Remove_ShouldKeepProfileAsFormerPlayer_WhenPlayerHasHistory()
        {
            GrantOrganizer();
            var player = Member("Jan", "Kowalski", 9);
            var past = Registration("Jesień", "Finished");
            InSquad(past.TournamentTeamId, player);

            var result = await RemoveHandler().Handle(new RemoveTeamPlayerCommand(_teamId, player.Id, false), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Deleted.Should().BeFalse();
            _members.Single().Status.Should().Be(TeamPlayerStatus.Left);
            await _dbContext.DidNotReceive().RemovePlayerAsync(Arg.Any<Player>(), Arg.Any<CancellationToken>());
            // Zakończony turniej zostaje nietknięty.
            await _dbContext.DidNotReceive().RemoveTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Remove_ShouldDeleteProfile_WhenPlayerNeverPlayed()
        {
            GrantOrganizer();
            var player = Member("Jan", "Kowalski", 9);

            var result = await RemoveHandler().Handle(new RemoveTeamPlayerCommand(_teamId, player.Id, false), CancellationToken.None);

            result.Value.Deleted.Should().BeTrue();
            await _dbContext.Received(1).RemoveTeamPlayerAsync(_members.Single(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemovePlayerAsync(player, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Remove_ShouldLeaveCurrentSquads_WhenNotAsked()
        {
            GrantOrganizer();
            var player = Member("Jan", "Kowalski", 9);
            var upcoming = Registration("Wiosna", "RegistrationOpen");
            InSquad(upcoming.TournamentTeamId, player);

            var result = await RemoveHandler().Handle(new RemoveTeamPlayerCommand(_teamId, player.Id, false), CancellationToken.None);

            result.Value.Deleted.Should().BeFalse();
            result.Value.RemovedFromSquads.Should().Be(0);
            await _dbContext.DidNotReceive().RemoveTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Remove_ShouldTakePlayerOffCurrentSquadsOnly_WhenAsked()
        {
            GrantOrganizer();
            var player = Member("Jan", "Kowalski", 9);
            var past = Registration("Jesień", "Finished");
            var upcoming = Registration("Wiosna", "RegistrationOpen");
            var pastEntry = InSquad(past.TournamentTeamId, player);
            var upcomingEntry = InSquad(upcoming.TournamentTeamId, player);
            _roles.Add((nameof(RoleScopeType.Tournament), upcoming.TournamentId, nameof(UserRole.Organizer)));

            var result = await RemoveHandler().Handle(new RemoveTeamPlayerCommand(_teamId, player.Id, true), CancellationToken.None);

            result.Value.RemovedFromSquads.Should().Be(1);
            result.Value.Deleted.Should().BeFalse();
            await _dbContext.Received(1).RemoveTournamentPlayerAsync(upcomingEntry, Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().RemoveTournamentPlayerAsync(pastEntry, Arg.Any<CancellationToken>());
            _members.Single().Status.Should().Be(TeamPlayerStatus.Left);
        }

        [Fact]
        public async Task Remove_ShouldKeepSquadLockedForCoach_AndReportIt()
        {
            var (tournamentTeamId, _) = Registration("Wiosna", "InProgress", canCoachEditAfterStart: false);
            _roles.Add((nameof(RoleScopeType.TournamentTeam), tournamentTeamId, nameof(UserRole.Coach)));
            var player = Member("Jan", "Kowalski", 9);
            InSquad(tournamentTeamId, player);

            var result = await RemoveHandler().Handle(new RemoveTeamPlayerCommand(_teamId, player.Id, true), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.KeptInSquads.Should().Equal("Wiosna");
            await _dbContext.DidNotReceive().RemoveTournamentPlayerAsync(Arg.Any<TournamentPlayer>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Remove_ShouldThrow_ForAssistantCoach()
        {
            var (tournamentTeamId, _) = Registration("Wiosna", "Finished");
            _roles.Add((nameof(RoleScopeType.TournamentTeam), tournamentTeamId, nameof(UserRole.AssistantCoach)));
            var player = Member("Jan", "Kowalski", 9);

            var act = () => RemoveHandler().Handle(new RemoveTeamPlayerCommand(_teamId, player.Id, false), CancellationToken.None);

            await act.Should().ThrowAsync<UnauthorizedException>();
        }

        // --- Przywracanie i synchronizacja ----------------------------------------------------------

        [Fact]
        public async Task Restore_ShouldDropNumber_WhenSomeoneElseHasIt()
        {
            GrantOrganizer();
            var former = Member("Jan", "Kowalski", 9, TeamPlayerStatus.Left);
            Member("Adam", "Nowak", 9);
            var handler = new RestoreTeamPlayerCommandHandler(_dbContext, AccessGuard(), _currentUser);

            var result = await handler.Handle(new RestoreTeamPlayerCommand(_teamId, former.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.ShirtNumber.Should().BeNull();
            _members.First().Status.Should().Be(TeamPlayerStatus.Active);
        }

        [Fact]
        public async Task Sync_ShouldAddNewAndBringBackFormerPlayers()
        {
            var former = Member("Jan", "Kowalski", 9, TeamPlayerStatus.Left);
            var newcomerId = Guid.NewGuid();

            var result = await new TeamRosterSync(_dbContext).EnsureInRosterAsync(_teamId, [(former.Id, 9), (newcomerId, 4)]);

            result.IsSuccess.Should().BeTrue();
            _members.Single().Status.Should().Be(TeamPlayerStatus.Active);
            await _dbContext.Received(1).AddTeamPlayerAsync(
                Arg.Is<TeamPlayer>(m => m.PlayerId == newcomerId && m.ShirtNumber == 4), Arg.Any<CancellationToken>());
        }
    }
}
