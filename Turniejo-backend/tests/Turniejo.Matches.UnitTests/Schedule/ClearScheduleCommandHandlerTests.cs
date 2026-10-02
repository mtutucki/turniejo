using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Schedule.ClearSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class ClearScheduleCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ITournamentModule _tournamentModule = Substitute.For<ITournamentModule>();
        private readonly IUserModule _userModule = Substitute.For<IUserModule>();
        private readonly ClearScheduleCommandHandler _handler;
        private readonly Guid _tournamentId = Guid.NewGuid();

        public ClearScheduleCommandHandlerTests()
        {
            _handler = new ClearScheduleCommandHandler(_dbContext, _tournamentModule, _userModule, Substitute.For<IBracketModule>());
            _tournamentModule.RevertScheduleGenerationAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(true);
        }

        private void TournamentIn(string status) =>
            _tournamentModule.GetTournamentAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(_tournamentId, "Turniej", status, "Group", Guid.NewGuid(), 16));

        private Match NewMatch()
        {
            var match = Match.Create(Guid.NewGuid(), _tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            match.ClearDomainEvents();
            return match;
        }

        private void Matches(params Match[] matches) =>
            _dbContext.GetMatchesByTournamentIdAsync(_tournamentId, Arg.Any<CancellationToken>()).Returns(matches.ToList());

        [Fact]
        public async Task Handle_ShouldRemoveMatches_RevokeReferees_AndRevertStage()
        {
            TournamentIn("ScheduleGenerated");
            var withReferee = NewMatch();
            var refereeId = Guid.NewGuid();
            withReferee.AssignReferee(refereeId, Guid.NewGuid());
            Matches(withReferee, NewMatch());

            var result = await _handler.Handle(new ClearScheduleCommand(_tournamentId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.MatchesRemoved.Should().Be(2);
            await _userModule.Received(1).RevokeRoleAsync(refereeId, "Referee", "Match", withReferee.Id, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).RemoveMatchesAsync(Arg.Is<IReadOnlyCollection<Match>>(m => m.Count == 2), Arg.Any<CancellationToken>());
            await _tournamentModule.Received(1).RevertScheduleGenerationAsync(_tournamentId, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCleanUpLeftovers_WithoutRevertingStage_WhenRegistrationClosed()
        {
            // Stan po nieudanym generowaniu: mecze zapisane, etap nie zmieniony.
            TournamentIn("RegistrationClosed");
            Matches(NewMatch());

            var result = await _handler.Handle(new ClearScheduleCommand(_tournamentId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).RemoveMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
            await _tournamentModule.DidNotReceive().RevertScheduleGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData("InProgress")]
        [InlineData("Finished")]
        [InlineData("RegistrationOpen")]
        public async Task Handle_ShouldFail_WhenTournamentIsNotInSchedulingStage(string status)
        {
            TournamentIn(status);

            var result = await _handler.Handle(new ClearScheduleCommand(_tournamentId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Status");
            await _dbContext.DidNotReceive().RemoveMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenAnyMatchHasStarted()
        {
            TournamentIn("ScheduleGenerated");
            var live = NewMatch();
            live.Start(Guid.NewGuid());
            Matches(NewMatch(), live);

            var result = await _handler.Handle(new ClearScheduleCommand(_tournamentId), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Schedule");
            await _dbContext.DidNotReceive().RemoveMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
            await _tournamentModule.DidNotReceive().RevertScheduleGenerationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldKeepBracketMatches_AndOnlyRevertStage_WhenFormatIsKnockout()
        {
            // Mecze pucharu należą do drabinki — czyszczenie terminarza ich nie usuwa.
            _tournamentModule.GetTournamentAsync(_tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(_tournamentId, "Puchar", "ScheduleGenerated", "Knockout", Guid.NewGuid(), 8));
            Matches(NewMatch(), NewMatch());

            var result = await _handler.Handle(new ClearScheduleCommand(_tournamentId), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Value.MatchesRemoved.Should().Be(0);
            await _dbContext.DidNotReceive().RemoveMatchesAsync(Arg.Any<IReadOnlyCollection<Match>>(), Arg.Any<CancellationToken>());
            await _tournamentModule.Received(1).RevertScheduleGenerationAsync(_tournamentId, Arg.Any<CancellationToken>());
        }
    }
}
