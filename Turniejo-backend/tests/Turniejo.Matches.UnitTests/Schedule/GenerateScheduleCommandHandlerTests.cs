using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Schedule.GenerateSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class GenerateScheduleCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly ITeamModule _teamModule;
        private readonly IBracketModule _bracketModule = Substitute.For<IBracketModule>();
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GenerateScheduleCommandHandler _handler;

        public GenerateScheduleCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _teamModule = Substitute.For<ITeamModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _handler = new GenerateScheduleCommandHandler(_dbContext, _tournamentModule, _teamModule, _bracketModule, _currentUserContext);
            _bracketModule.GetNodesByTournamentIdAsync(default, default).ReturnsForAnyArgs(new List<BracketNodeInfoDto>());
        }

        private static TournamentDto FakeTournament(Guid tournamentId, string? format = "Group") =>
            new(tournamentId, "Turniej", "RegistrationClosed", format, Guid.NewGuid(), 16);

        private static TournamentSettingsSummaryDto FakeSettings(Guid tournamentId) =>
            new(tournamentId, 30, 10, 60, 3, 1, 0, false, false, 3, 0);

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
            _tournamentModule.ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>())
                .Returns(_ =>
                {
                    _saveCountWhenStatusChanged = _dbContext.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "SaveChangesAsync");
                    return true;
                });
            _currentUserContext.UserId.Returns(Guid.NewGuid());
        }

        private int _saveCountWhenStatusChanged = -1;

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
            // Najpierw mecze, potem (po zmianie etapu) zdarzenie ScheduleGenerated w outboxie.
            await _dbContext.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotScheduleSameClubInTwoCategoriesAtTheSameTime()
        {
            // Ta sama drużyna z bazy zgłoszona do dwóch kategorii to dwa zgłoszenia (różne TournamentTeamId),
            // ale wspólny trener i często zawodnicy — mimo dwóch wolnych boisk jej mecze nie mogą się nakładać.
            var tournamentId = Guid.NewGuid();
            var sharedClubId = Guid.NewGuid();
            var (categoryA, groupA) = (Guid.NewGuid(), Guid.NewGuid());
            var (categoryB, groupB) = (Guid.NewGuid(), Guid.NewGuid());
            var pitchIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeSettings(tournamentId));
            _tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentGroupDto>
                {
                    new(groupA, tournamentId, categoryA, "Grupa A"),
                    new(groupB, tournamentId, categoryB, "Grupa A"),
                });
            _dbContext.GetPitchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(pitchIds.Select((id, i) => Pitch.Create(id, tournamentId, $"Boisko {i + 1}", null, Guid.NewGuid()).Value).ToList());
            _teamModule.GetTournamentTeamsByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<TournamentTeamDto>
                {
                    new(Guid.NewGuid(), tournamentId, sharedClubId, categoryA, groupA, "Accepted"),
                    new(Guid.NewGuid(), tournamentId, Guid.NewGuid(), categoryA, groupA, "Accepted"),
                    new(Guid.NewGuid(), tournamentId, sharedClubId, categoryB, groupB, "Accepted"),
                    new(Guid.NewGuid(), tournamentId, Guid.NewGuid(), categoryB, groupB, "Accepted"),
                });
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match>());
            _tournamentModule.ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>()).Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            IReadOnlyCollection<Match>? created = null;
            await _dbContext.AddMatchesAsync(Arg.Do<IReadOnlyCollection<Match>>(m => created = m), Arg.Any<CancellationToken>());

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var result = await _handler.Handle(
                new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), pitchIds), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            created.Should().HaveCount(2);
            created!.Select(m => m.ScheduledAt).Should().OnlyHaveUniqueItems();
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

        private List<Match> SetupKnockout(Guid tournamentId, Guid pitchId, int firstRoundMatches)
        {
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeTournament(tournamentId, "Knockout"));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(FakeSettings(tournamentId));
            var pitch = Pitch.Create(pitchId, tournamentId, "Boisko 1", null, Guid.NewGuid()).Value;
            _dbContext.GetPitchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(new List<Pitch> { pitch });

            // Mecze 1. rundy utworzone przez drabinkę — z terminem "na teraz" i bez boiska.
            var created = DateTimeOffset.UtcNow;
            var matches = Enumerable.Range(0, firstRoundMatches)
                .Select(i => Match.Create(Guid.NewGuid(), tournamentId, Guid.NewGuid(), null,
                    Guid.NewGuid(), Guid.NewGuid(), null, created.AddSeconds(i), StageType.Knockout).Value)
                .ToList();
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(matches);
            _dbContext.GetTrackedMatchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(call => matches.Where(m => call.Arg<IReadOnlyCollection<Guid>>().Contains(m.Id)).ToList());
            _tournamentModule.ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>()).Returns(true);
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            return matches;
        }

        [Fact]
        public async Task Handle_ShouldPlanLaterRounds_AfterTheMatchesThatFeedThem()
        {
            // Półfinały (2 mecze) + finał, który jeszcze nie ma meczu: finał dostaje termin po obu półfinałach.
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var matches = SetupKnockout(tournamentId, pitchId, 2);
            var semi1 = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, matches[0].Id, null, null, false);
            var semi2 = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, matches[1].Id, null, null, false);
            var final = new BracketNodeInfoDto(Guid.NewGuid(), 2, false, null, semi1.Id, semi2.Id, false);
            _bracketModule.GetNodesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<BracketNodeInfoDto> { final, semi1, semi2 });
            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

            // Act
            var result = await _handler.Handle(
                new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(4), new[] { pitchId }), CancellationToken.None);

            // Assert — półfinały 10:00 i 10:40, finał w kolejnym slocie 11:20.
            result.IsSuccess.Should().BeTrue();
            await _bracketModule.Received(1).SetPlannedSlotsAsync(
                Arg.Is<IReadOnlyCollection<BracketPlannedSlotDto>>(slots =>
                    slots.Count == 1 && slots.Single().NodeId == final.Id &&
                    slots.Single().PitchId == pitchId && slots.Single().ScheduledAt == startAt.AddMinutes(80)),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenLaterRoundsDoNotFitInTheWindow()
        {
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var matches = SetupKnockout(tournamentId, pitchId, 2);
            var semi1 = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, matches[0].Id, null, null, false);
            var semi2 = new BracketNodeInfoDto(Guid.NewGuid(), 1, false, matches[1].Id, null, null, false);
            var final = new BracketNodeInfoDto(Guid.NewGuid(), 2, false, null, semi1.Id, semi2.Id, false);
            _bracketModule.GetNodesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<BracketNodeInfoDto> { semi1, semi2, final });
            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);

            // Act — okno mieści tylko dwa półfinały
            var result = await _handler.Handle(
                new GenerateScheduleCommand(tournamentId, startAt, startAt.AddMinutes(80), new[] { pitchId }), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.EndAt");
            await _bracketModule.DidNotReceive().SetPlannedSlotsAsync(
                Arg.Any<IReadOnlyCollection<BracketPlannedSlotDto>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldScheduleBracketFirstRound_WhenFormatIsKnockout()
        {
            // Arrange — ćwierćfinały: 4 mecze, jedno boisko, slot 30 + 10 min
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var matches = SetupKnockout(tournamentId, pitchId, 4);
            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(4), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert — bez nowych meczów: istniejące dostają kolejne sloty na boisku i etap się zmienia.
            result.IsSuccess.Should().BeTrue();
            result.Value.MatchesCreated.Should().Be(4);
            await _dbContext.DidNotReceive().AddMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
            matches.Select(m => m.ScheduledAt).Should().Equal(
                startAt, startAt.AddMinutes(40), startAt.AddMinutes(80), startAt.AddMinutes(120));
            matches.Should().OnlyContain(m => m.PitchId == pitchId);
            await _tournamentModule.Received(1).ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenKnockoutHasNoBracketYet()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupKnockout(tournamentId, pitchId, 0);
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Bracket");
            await _tournamentModule.DidNotReceive().ChangeTournamentStatusAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldRestoreKnockoutTimes_WhenStageCannotBeChanged()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            var matches = SetupKnockout(tournamentId, pitchId, 2);
            var before = matches.Select(m => (m.PitchId, m.ScheduledAt)).ToList();
            _tournamentModule.ChangeTournamentStatusAsync(tournamentId, "ScheduleGenerated", Arg.Any<CancellationToken>()).Returns(false);
            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(4), new[] { pitchId });

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            matches.Select(m => (m.PitchId, m.ScheduledAt)).Should().Equal(before);
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
            // Mecze zapisane przed zmianą etapu są wycofywane, a zdarzenie ScheduleGenerated nie trafia do outboxa.
            await _dbContext.Received(1).RemoveMatchesAsync(
                Arg.Is<IReadOnlyCollection<Match>>(m => m.Count == 1), Arg.Any<CancellationToken>());
            await _dbContext.DidNotReceive().AddOutboxMessageAsync(Arg.Any<Turniejo.SharedKernel.Outbox.OutboxMessage>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("Draft")]
        [InlineData("RegistrationOpen")]
        [InlineData("ScheduleGenerated")]
        public async Task Handle_ShouldFail_WhenRegistrationIsNotClosed(string status)
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Turniej", status, "Group", Guid.NewGuid(), 16));
            var command = new GenerateScheduleCommand(tournamentId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), new[] { Guid.NewGuid() });

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            await _tournamentModule.DidNotReceive().ChangeTournamentStatusAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNeverScheduleTeamTwiceInTheSameSlot_WhenMultiplePitches()
        {
            // 4 drużyny, 2 boiska — naiwne przydzielanie par po kolei dawało (A-B) i (A-C) w tym samym slocie.
            var tournamentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var teamIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            SetupHappyPath(tournamentId, Guid.NewGuid(), groupId, categoryId, teamIds);
            var pitches = new List<Pitch>
            {
                Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 1", null, Guid.NewGuid()).Value,
                Pitch.Create(Guid.NewGuid(), tournamentId, "Boisko 2", null, Guid.NewGuid()).Value
            };
            _dbContext.GetPitchesByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(pitches);
            IReadOnlyCollection<Match>? created = null;
            await _dbContext.AddMatchesAsync(Arg.Do<IReadOnlyCollection<Match>>(m => created = m), Arg.Any<CancellationToken>());

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), pitches.Select(p => p.Id).ToArray());

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            created.Should().HaveCount(6);
            foreach (var slot in created!.GroupBy(m => m.ScheduledAt))
            {
                var teamsInSlot = slot.SelectMany(m => new[] { m.HomeTournamentTeamId, m.AwayTournamentTeamId }).ToList();
                teamsInSlot.Should().OnlyHaveUniqueItems();
                slot.Select(m => m.PitchId).Should().OnlyHaveUniqueItems();
            }

            // Każda para dokładnie raz.
            created.Select(m => string.Join("|", new[] { m.HomeTournamentTeamId, m.AwayTournamentTeamId }.OrderBy(x => x)))
                .Should().OnlyHaveUniqueItems();
            // Metoda kołowa na 2 boiskach: 3 kolejki po 2 mecze.
            created.Select(m => m.ScheduledAt).Distinct().Should().HaveCount(3);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenLastMatchWouldEndAfterEndAt()
        {
            // 3 drużyny, 1 boisko, sloty 10:00 / 10:40 / 11:20 (30 min meczu + 10 przerwy).
            // Okno do 11:30: trzeci mecz zaczynałby się przed końcem, ale kończył o 11:50.
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddMinutes(90), new[] { pitchId });

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("1 match(es) could not be placed");
        }

        [Fact]
        public async Task Handle_ShouldSpreadMatchesAcrossDays_WhenWindowsAreGiven()
        {
            // 4 drużyny = 6 meczów, 1 boisko. Każdy dzień 10:00–12:00 mieści 3 mecze (10:00 / 10:40 / 11:20),
            // więc druga połowa trafia na kolejny dzień — nic nie ląduje w nocy między oknami.
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, Guid.NewGuid(), Guid.NewGuid(),
                new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

            var day1 = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var day2 = day1.AddDays(1);
            var command = new GenerateScheduleCommand(tournamentId, day1, day2.AddHours(2), new[] { pitchId },
                new[] { new ScheduleWindow(day1, day1.AddHours(2)), new ScheduleWindow(day2, day2.AddHours(2)) });

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            DateTimeOffset[] expected =
            [
                day1, day1.AddMinutes(40), day1.AddMinutes(80),
                day2, day2.AddMinutes(40), day2.AddMinutes(80),
            ];
            await _dbContext.Received(1).AddMatchesAsync(
                Arg.Is<IReadOnlyCollection<Match>>(m => m.Select(x => x.ScheduledAt).OrderBy(x => x).SequenceEqual(expected)),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenAWindowIsOutsideTheTournamentDates()
        {
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
            var day1 = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(FakeTournament(tournamentId) with { StartAt = day1, EndAt = day1.AddHours(8) });

            var command = new GenerateScheduleCommand(tournamentId, day1, day1.AddHours(8), new[] { pitchId },
                new[] { new ScheduleWindow(day1, day1.AddHours(2)), new ScheduleWindow(day1.AddDays(1), day1.AddDays(1).AddHours(2)) });

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.StartAt");
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenLastMatchEndsExactlyAtEndAt()
        {
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var command = new GenerateScheduleCommand(tournamentId, startAt, startAt.AddMinutes(110), new[] { pitchId });

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.MatchesCreated.Should().Be(3);
        }
        [Fact]
        public async Task Handle_ShouldFail_WhenGroupMatchesAlreadyExist()
        {
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });
            var leftover = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, DateTimeOffset.UtcNow, Turniejo.Matches.Domain.Enums.StageType.Group).Value;
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { leftover });

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            var result = await _handler.Handle(new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), new[] { pitchId }), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Message.Should().Contain("clear the schedule first");
            await _dbContext.DidNotReceive().AddMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenWindowIsOutsideTournamentDates()
        {
            var tournamentId = Guid.NewGuid();
            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "T", "RegistrationClosed", "Group", Guid.NewGuid(), 16, startAt, startAt.AddHours(4)));

            var result = await _handler.Handle(new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), new[] { Guid.NewGuid() }), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.StartAt");
        }

        [Fact]
        public async Task Handle_ShouldSaveMatchesBeforeChangingStage_AndPublishEventAfterwards()
        {
            var tournamentId = Guid.NewGuid();
            var pitchId = Guid.NewGuid();
            SetupHappyPath(tournamentId, pitchId, Guid.NewGuid(), Guid.NewGuid(), new List<Guid> { Guid.NewGuid(), Guid.NewGuid() });

            var startAt = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
            await _handler.Handle(new GenerateScheduleCommand(tournamentId, startAt, startAt.AddHours(6), new[] { pitchId }), CancellationToken.None);

            var calls = _dbContext.ReceivedCalls().Concat(_tournamentModule.ReceivedCalls())
                .Select(c => c.GetMethodInfo().Name)
                .Where(n => n is "AddMatchesAsync" or "SaveChangesAsync" or "ChangeTournamentStatusAsync" or "AddOutboxMessageAsync")
                .ToList();
            // ReceivedCalls() dwóch substytutów nie da się scalić chronologicznie, więc sprawdzamy kolejność
            // w bazie meczów, a zmianę etapu — względem niej przez licznik zapisów w chwili wywołania.
            calls.Take(4).Should().Equal("AddMatchesAsync", "SaveChangesAsync", "AddOutboxMessageAsync", "SaveChangesAsync");
            _saveCountWhenStatusChanged.Should().Be(1);
        }
    }
}
