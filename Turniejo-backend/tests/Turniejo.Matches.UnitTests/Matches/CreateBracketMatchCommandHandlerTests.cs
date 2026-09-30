using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.CreateBracketMatch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class CreateBracketMatchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly CreateBracketMatchCommandHandler _handler;

        public CreateBracketMatchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new CreateBracketMatchCommandHandler(_dbContext, _tournamentModule);
            _dbContext.GetMatchesByTournamentIdAsync(default, default).ReturnsForAnyArgs(new List<Match>());
            _dbContext.GetPitchesByTournamentIdAsync(default, default).ReturnsForAnyArgs(new List<Pitch>());
            _tournamentModule.GetTournamentSettingsAsync(default, default).ReturnsForAnyArgs(
                ci => new TournamentSettingsSummaryDto(ci.Arg<Guid>(), 30, 10, 60, 3, 1, 0, false, false, 3, 0));
        }

        [Fact]
        public async Task Handle_ShouldCreateAndPersistMatch_WhenDataIsValid()
        {
            // Zamyka lukę z audytu testów: jedyny handler w backendzie bez żadnego pokrycia, mimo że
            // leży na hot path całego mechanizmu awansu w drabince (BracketAdvancer.TryCreateMatchAsync).
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var homeTeamId = Guid.NewGuid();
            var awayTeamId = Guid.NewGuid();
            var command = new CreateBracketMatchCommand(tournamentId, categoryId, homeTeamId, awayTeamId, StageType.Knockout);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddMatchAsync(
                Arg.Is<Match>(m =>
                    m.TournamentId == tournamentId && m.CategoryId == categoryId &&
                    m.HomeTournamentTeamId == homeTeamId && m.AwayTournamentTeamId == awayTeamId &&
                    m.StageType == StageType.Knockout && m.Status == MatchStatus.Scheduled),
                Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldReturnCreatedMatchId()
        {
            var command = new CreateBracketMatchCommand(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), StageType.Final);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchId.Should().NotBe(Guid.Empty);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenHomeAndAwayTeamsAreTheSame()
        {
            // Match.Create odrzuca drużynę graną samą przeciwko sobie — handler musi to poprawnie
            // propagować jako Result.Failure, nie zapisywać nic do bazy.
            var teamId = Guid.NewGuid();
            var command = new CreateBracketMatchCommand(Guid.NewGuid(), Guid.NewGuid(), teamId, teamId, StageType.Knockout);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.AwayTournamentTeamId");
            await _dbContext.DidNotReceive().AddMatchAsync(Arg.Any<Match>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
        [Fact]
        public async Task Handle_ShouldScheduleAfterGroupStage_WithAPitch()
        {
            // Wcześniej mecz drabinki dostawał termin "teraz" i brak boiska.
            var tournamentId = Guid.NewGuid();
            var start = DateTimeOffset.UtcNow.AddHours(2);
            var busyPitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            var freePitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 2", null, Guid.NewGuid()).Value;
            var groupOnBusy = Match.Create(Guid.NewGuid(), tournamentId, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), busyPitch.Id, start, StageType.Group).Value;
            var lastGroup = Match.Create(Guid.NewGuid(), tournamentId, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), busyPitch.Id, start.AddMinutes(40), StageType.Group).Value;
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { groupOnBusy, lastGroup });
            _dbContext.GetPitchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Pitch> { busyPitch, freePitch });

            await _handler.Handle(new CreateBracketMatchCommand(tournamentId, null, Guid.NewGuid(), Guid.NewGuid(), StageType.Knockout), CancellationToken.None);

            // Koniec fazy grupowej: ostatni mecz 10:40 + 30 min gry + 10 min przerwy.
            await _dbContext.Received(1).AddMatchAsync(
                // Oba boiska są wolne od końca fazy grupowej — ważne, że mecz ma termin po niej i boisko.
                Arg.Is<Match>(m => m.ScheduledAt == start.AddMinutes(80) && m.PitchId.HasValue),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldWaitForTeamsPreviousMatch()
        {
            var tournamentId = Guid.NewGuid();
            var team = Guid.NewGuid();
            var start = DateTimeOffset.UtcNow.AddHours(3);
            var pitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            var otherPitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 2", null, Guid.NewGuid()).Value;
            var semi = Match.Create(Guid.NewGuid(), tournamentId, null, null, team, Guid.NewGuid(), pitch.Id, start, StageType.Knockout).Value;
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { semi });
            _dbContext.GetPitchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Pitch> { pitch, otherPitch });

            await _handler.Handle(new CreateBracketMatchCommand(tournamentId, null, team, Guid.NewGuid(), StageType.Final), CancellationToken.None);

            await _dbContext.Received(1).AddMatchAsync(
                Arg.Is<Match>(m => m.ScheduledAt == start.AddMinutes(40)), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotScheduleBeforeTournamentStart()
        {
            // Puchar: drabinka powstaje przed turniejem — mecz 1. rundy nie może dostać terminu "teraz".
            var tournamentId = Guid.NewGuid();
            var tournamentStart = DateTimeOffset.UtcNow.AddDays(7);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Puchar", "RegistrationClosed", "Knockout", Guid.NewGuid(), 8, tournamentStart, tournamentStart.AddHours(8)));

            await _handler.Handle(new CreateBracketMatchCommand(tournamentId, null, Guid.NewGuid(), Guid.NewGuid(), StageType.Knockout), CancellationToken.None);

            await _dbContext.Received(1).AddMatchAsync(
                Arg.Is<Match>(m => m.ScheduledAt == tournamentStart), Arg.Any<CancellationToken>());
        }
    }
}
