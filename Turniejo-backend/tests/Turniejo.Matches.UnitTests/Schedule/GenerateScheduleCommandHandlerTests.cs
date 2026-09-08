using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Schedule.GenerateSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class GenerateScheduleCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly ITeamModule _teamModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GenerateScheduleCommandHandler _handler;

        public GenerateScheduleCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new GenerateScheduleCommandHandler(_dbContext, _tournamentModule, _teamModule, _currentUserContext);
        }

        private static TournamentDto FakeTournament(Guid tournamentId, string? format = "Group") =>
            new(tournamentId, "Turniej", "RegistrationClosed", format, Guid.NewGuid(), 16);

        private static TournamentSettingsSummaryDto FakeSettings(Guid tournamentId) =>
            new(tournamentId, 30, 10, 60, 3, 1, 0, false, false);

        private void SetupHappyPath(Guid tournamentId, Guid pitchId, Guid groupId, Guid categoryId, List<Guid> acceptedTeamIds)
        {
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeSettings(tournamentId));
            _tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentGroupDto> { new(groupId, tournamentId, categoryId, "Grupa A") });

            var pitch = Pitch.Create(pitchId, tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            _dbContext.GetPitchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Pitch> { pitch });

            var teamDtos = acceptedTeamIds
                .Select(id => new TournamentTeamDto(id, tournamentId, Guid.NewGuid(), categoryId, groupId, "Accepted"))
                .ToList();
            _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(teamDtos);

            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match>());
            _tournamentModule.ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>()).Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_AndCreateRoundRobinMatches_WhenDataIsValid()
        {
            // Arrange — 3 zaakceptowane drużyny w jednej grupie => C(3,2) = 3 mecze
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var teamIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            SetupHappyPath(tournamentId, pitchId, groupId, categoryId, teamIds);

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchesCreated.Should().Be(3);
            await _dbContext.Received(1).AddMatchesAsync(
                Arg.Is<IReadOnlyCollection<Match>>(m => m.Count == 3), Arg.Any<CancellationToken>());
            await _tournamentModule.Received(1).ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentNotFound()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns((TournamentDto?)null);
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { Guid.NewGuid() });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Tournament.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenFormatIsKnockout()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId, "Knockout"));
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { Guid.NewGuid() });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Format");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenFormatIsNotSet()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId, null));
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { Guid.NewGuid() });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Format");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNoValidActivePitchesProvided()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeSettings(tournamentId));
            _dbContext.GetPitchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Pitch>());
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { Guid.NewGuid() });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.PitchIds");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNoGroupsDefined()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitch = Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeSettings(tournamentId));
            _dbContext.GetPitchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Pitch> { pitch });
            _tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentGroupDto>());
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { pitch.Id });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("no groups");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNotEnoughAcceptedTeamsInAnyGroup()
        {
            // Arrange — tylko 1 zaakceptowana drużyna w grupie, brak par do rozegrania
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, groupId, categoryId, new List<Guid> { Guid.NewGuid() });
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(6), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("No matches to schedule");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNotEnoughTimeToScheduleAllMatches()
        {
            // Arrange — okno czasowe krótsze niż jeden slot (30+10=40 min)
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var teamIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            SetupHappyPath(tournamentId, pitchId, groupId, categoryId, teamIds);

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddMinutes(30), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("Not enough time");
            await _dbContext.DidNotReceive().AddMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenStatusTransitionFails()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var teamIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            SetupHappyPath(tournamentId, pitchId, groupId, categoryId, teamIds);
            _tournamentModule.ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>()).Returns(false);

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Status");
            await _dbContext.DidNotReceive().AddMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }
    }
}
