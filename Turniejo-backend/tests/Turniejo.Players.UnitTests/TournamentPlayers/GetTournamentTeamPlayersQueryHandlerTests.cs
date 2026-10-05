using FluentAssertions;
using NSubstitute;
using Turniejo.Players.Application.Abstractions;
using Turniejo.Players.Application.TournamentPlayers.GetTournamentTeamPlayers;
using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Players.UnitTests.TournamentPlayers
{
    public sealed class GetTournamentTeamPlayersQueryHandlerTests
    {
        private readonly IPlayersDbContext _dbContext;
        private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
        private readonly GetTournamentTeamPlayersQueryHandler _handler;
        private static readonly Guid TournamentId = Guid.NewGuid();

        public GetTournamentTeamPlayersQueryHandlerTests()
        {
            _dbContext = Substitute.For<IPlayersDbContext>();
            _handler = new GetTournamentTeamPlayersQueryHandler(_dbContext, _teamModule);
            // domyślnie: drużyna należy do turnieju z adresu
            _teamModule.GetTournamentTeamAsync(default, default).ReturnsForAnyArgs(
                ci => new TournamentTeamDto(ci.Arg<Guid>(), TournamentId, Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_WhenPlayersExist()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, Guid.NewGuid(), 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { tournamentPlayer });

            // Act
            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(TournamentId, tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle();
            result.Value[0].Id.Should().Be(tournamentPlayer.Id);
            result.Value[0].ShirtNumber.Should().Be(9);
        }

        [Fact]
        public async Task Handle_ShouldIncludePlayerProfile_WhenPlayerExists()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            var birthDate = new DateTime(2014, 5, 1);
            var player = Player.Create(Guid.NewGuid(), "Adam", "Nowak", birthDate, PlayerPosition.Forward, null, null, Guid.NewGuid()).Value;
            var tournamentPlayer = TournamentPlayer.Create(Guid.NewGuid(), tournamentTeamId, player.Id, 9, PlayerPosition.Forward, Guid.NewGuid()).Value;
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer> { tournamentPlayer });
            _dbContext.GetPlayersByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Player> { player });

            // Act
            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(TournamentId, tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value[0].FirstName.Should().Be("Adam");
            result.Value[0].LastName.Should().Be("Nowak");
            result.Value[0].BirthDate.Should().Be(birthDate);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoPlayersExist()
        {
            // Arrange
            var tournamentTeamId = Guid.NewGuid();
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(tournamentTeamId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentPlayer>());

            // Act
            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(TournamentId, tournamentTeamId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }

        [Fact]
        public void AlternateAllowedRoles_ShouldMatchRestOfCoachPanel_IncludingAssistantCoachAndManager()
        {
            // Zamyka niespójność z audytu: reszta panelu trenera (harmonogram/wyniki/statystyki)
            // dopuszcza AssistantCoach/Manager (TeamRoles.AnyTeamStaff), a lista zawodników wymagała
            // dotąd samego Coacha — jedyny widok panelu niedostępny dla reszty sztabu.
            var query = new GetTournamentTeamPlayersQuery(TournamentId, Guid.NewGuid());

            query.AlternateAllowedRoles.Should().BeEquivalentTo(["Coach", "AssistantCoach", "Manager"]);
        }
        [Fact]
        public async Task Handle_ShouldReturnNotFound_WhenTeamBelongsToAnotherTournament()
        {
            // Kibic turnieju A nie może czytać składu (z datami urodzenia) drużyny z turnieju B.
            var foreignTeamId = Guid.NewGuid();
            _teamModule.GetTournamentTeamAsync(foreignTeamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(foreignTeamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Accepted"));

            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(TournamentId, foreignTeamId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("TournamentTeam.NotFound");
            await _dbContext.DidNotReceive().GetTournamentPlayersByTournamentTeamIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSkipTournamentCheck_ForCoachRoute()
        {
            // /api/coach/teams/{id}/players przekazuje Guid.Empty — dostęp daje tam tylko Coach@TournamentTeam.
            var teamId = Guid.NewGuid();
            _dbContext.GetTournamentPlayersByTournamentTeamIdAsync(teamId, Arg.Any<CancellationToken>()).Returns(new List<TournamentPlayer>());

            var result = await _handler.Handle(new GetTournamentTeamPlayersQuery(Guid.Empty, teamId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _teamModule.DidNotReceive().GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
