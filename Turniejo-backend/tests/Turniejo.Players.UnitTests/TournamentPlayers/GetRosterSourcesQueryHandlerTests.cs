using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.TournamentPlayers.GetRosterSources;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class GetRosterSourcesQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ITournamentModule _tournamentModule;
        private readonly GetRosterSourcesQueryHandler _handler;

        private readonly Guid _tournamentId = Guid.NewGuid();
        private readonly Guid _tournamentTeamId = Guid.NewGuid();
        private readonly Guid _teamId = Guid.NewGuid();
        private readonly Guid _categoryId = Guid.NewGuid();
        private readonly List<Player> _profiles = [];
        private readonly List<TournamentTeamDto> _registrations = [];

        public GetRosterSourcesQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new GetRosterSourcesQueryHandler(_dbContext, _teamModule, _tournamentModule);

            var current = new TournamentTeamDto(_tournamentTeamId, _tournamentId, _teamId, _categoryId, null, "Accepted");
            _registrations.Add(current);
            _teamModule.GetTournamentTeamAsync(_tournamentTeamId, Arg.Any<CancellationToken>()).Returns(current);
            _teamModule.GetTournamentTeamsByTeamIdAsync(_teamId, Arg.Any<CancellationToken>()).Returns(_registrations);
            _tournamentModule.GetTournamentCategoryAsync(_categoryId, Arg.Any<CancellationToken>())
                .Returns(new TournamentCategoryDto(_categoryId, _tournamentId, "U12", new DateTime(2011, 1, 1), new DateTime(2013, 1, 1)));

            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer>());
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(call => _profiles.Where(p => call.Arg<IReadOnlyCollection<Guid>>().Contains(p.Id)).ToList());
        }

        private Player Profile(string firstName, string lastName, DateTime birthDate)
        {
            var player = Player.Create(Guid.NewGuid(), firstName, lastName, birthDate, PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            _profiles.Add(player);
            return player;
        }

        private Guid PreviousRegistration(string tournamentName, DateTimeOffset startAt, params TournamentPlayer[] squad)
        {
            var id = squad.Length > 0 ? squad[0].TournamentTeamId : Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            _registrations.Add(new TournamentTeamDto(id, tournamentId, _teamId, Guid.NewGuid(), null, "Accepted"));
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, tournamentName, "Finished", null, Guid.NewGuid(), 16, startAt, startAt.AddDays(1)));
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(id, Arg.Any<CancellationToken>()).Returns(squad.ToList());
            return id;
        }

        private static TournamentPlayer SquadEntry(Guid tournamentTeamId, Guid playerId, int? shirtNumber) =>
            TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, playerId, shirtNumber, PlayerPosition.Defender, Guid.NewGuid()).Value;

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentTeamBelongsToDifferentTournament()
        {
            var result = await _handler.Handle(new GetRosterSourcesQuery(Guid.NewGuid(), _tournamentTeamId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnPreviousSquadsNewestFirst_WithoutCurrentRegistration()
        {
            var jan = Profile("Jan", "Kowalski", new DateTime(2012, 5, 1));
            var older = Guid.NewGuid();
            var newer = Guid.NewGuid();
            PreviousRegistration("Wiosna", new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), SquadEntry(older, jan.Id, 9));
            PreviousRegistration("Lato", new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), SquadEntry(newer, jan.Id, 10));

            var result = await _handler.Handle(new GetRosterSourcesQuery(_tournamentId, _tournamentTeamId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Select(s => s.TournamentName).Should().Equal("Lato", "Wiosna");
            result.Value.Select(s => s.TournamentTeamId).Should().Equal(newer, older);
            result.Value[0].Players.Should().ContainSingle()
                .Which.Should().BeEquivalentTo(new { PlayerId = jan.Id, ShirtNumber = 10, Position = "Defender", AlreadyInSquad = false, Ineligibility = (string?)null });
        }

        [Fact]
        public async Task Handle_ShouldFlagPlayersAlreadyInSquadAndOutsideCategory()
        {
            var jan = Profile("Jan", "Kowalski", new DateTime(2012, 5, 1));
            var tooOld = Profile("Adam", "Stary", new DateTime(2010, 6, 1));
            var tooYoung = Profile("Olek", "Mlody", new DateTime(2014, 6, 1));
            var source = Guid.NewGuid();
            PreviousRegistration("Wiosna", DateTimeOffset.UtcNow.AddMonths(-3),
                SquadEntry(source, jan.Id, 1), SquadEntry(source, tooOld.Id, 2), SquadEntry(source, tooYoung.Id, 3));
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(_tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { SquadEntry(_tournamentTeamId, jan.Id, 1) });

            var result = await _handler.Handle(new GetRosterSourcesQuery(_tournamentId, _tournamentTeamId), CancellationToken.None);

            var players = result.Value.Single().Players;
            players.Single(p => p.PlayerId == jan.Id).AlreadyInSquad.Should().BeTrue();
            players.Single(p => p.PlayerId == tooOld.Id).Ineligibility.Should().Be("TooOld");
            players.Single(p => p.PlayerId == tooYoung.Id).Ineligibility.Should().Be("TooYoung");
        }

        [Fact]
        public async Task Handle_ShouldSkipRemovedPlayersAndEmptySquads()
        {
            var jan = Profile("Jan", "Kowalski", new DateTime(2012, 5, 1));
            var source = Guid.NewGuid();
            var removed = SquadEntry(source, jan.Id, 9);
            removed.UpdateDetails(9, null, TournamentPlayerStatus.Removed);
            PreviousRegistration("Wiosna", DateTimeOffset.UtcNow.AddMonths(-3), removed);
            PreviousRegistration("Zima", DateTimeOffset.UtcNow.AddMonths(-8));

            var result = await _handler.Handle(new GetRosterSourcesQuery(_tournamentId, _tournamentTeamId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
            // QA v3: DRU-027a - zawodnik usunięty z kadry z opcją „Zostaw w składzie” dalej był proponowany
        // (i zaznaczony) w źródle „skład z poprzedniego turnieju”. Były zawodnik kadry nie jest proponowany.
        [Fact]
        public async Task DRU_027a_FormerRosterMember_IsNotOfferedFromPreviousSquads()
        {
            var former = Profile("Bruno", "Były", new DateTime(2012, 5, 1));
            var current = Profile("Antek", "Obecny", new DateTime(2012, 6, 1));
            var registration = Guid.NewGuid();
            PreviousRegistration("Wiosna", DateTimeOffset.UtcNow.AddMonths(-2),
                SquadEntry(registration, former.Id, 8), SquadEntry(registration, current.Id, 7));
            var leftMember = TeamPlayer.Create(Guid.NewGuid(), _teamId, former.Id, 8).Value;
            leftMember.Leave();
            _dbContext.GetTeamPlayersByTeamIdAsync(_teamId, Arg.Any<CancellationToken>())
                .Returns([leftMember, TeamPlayer.Create(Guid.NewGuid(), _teamId, current.Id, 7).Value]);

            var result = await _handler.Handle(new GetRosterSourcesQuery(_tournamentId, _tournamentTeamId), CancellationToken.None);

            var previous = result.Value.Single(s => s.Kind == "Tournament");
            previous.Players.Select(p => p.PlayerId).Should().Equal(current.Id);
        }
    }
}
