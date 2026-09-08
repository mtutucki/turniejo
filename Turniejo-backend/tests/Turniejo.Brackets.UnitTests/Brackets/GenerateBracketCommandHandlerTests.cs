using FluentAssertions;
using NSubstitute;
using Turniejo.Brackets.Application.Abstractions;
using Turniejo.Brackets.Application.Brackets.GenerateBracket;
using Turniejo.Brackets.Application.Services;
using Turniejo.Brackets.Domain.Entities;
using Turniejo.Brackets.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Brackets.UnitTests.Brackets
{
    public sealed class GenerateBracketCommandHandlerTests
    {
        private readonly IBracketsDbContext _dbContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly IMatchModule _matchModule;
        private readonly IStandingModule _standingModule;
        private readonly GenerateBracketCommandHandler _handler;

        public GenerateBracketCommandHandlerTests()
        {
            _dbContext = Substitute.For<IBracketsDbContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _matchModule = Substitute.For<IMatchModule>();
            _standingModule = Substitute.For<IStandingModule>();
            var advancer = new BracketAdvancer(_matchModule, _standingModule);
            _handler = new GenerateBracketCommandHandler(_dbContext, _tournamentModule, advancer);

            _dbContext.GetBracketByTournamentAndCategoryAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns((Bracket?)null);
            _matchModule.CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => Guid.NewGuid());
        }

        private static BracketPairingInput FixedPairing() =>
            new(BracketSourceType.Fixed, null, null, Guid.NewGuid(), BracketSourceType.Fixed, null, null, Guid.NewGuid());

        [Fact]
        public async Task Handle_ShouldCreateThreeRounds_ForFourFixedPairings()
        {
            // Arrange: 4 pairings (8 teams) -> Round 1 (4), Round 2/Semis (2), Round 3/Final (1) = 3 rounds.
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Knockout", Guid.NewGuid(), 8));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false));

            var pairings = new List<BracketPairingInput> { FixedPairing(), FixedPairing(), FixedPairing(), FixedPairing() };
            var command = new GenerateBracketCommand(tournamentId, categoryId, pairings);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.RoundCount.Should().Be(3);
            await _dbContext.Received(3).AddBracketRoundAsync(Arg.Any<BracketRound>(), Arg.Any<CancellationToken>());
            // 4 round-1 + 2 round-2 + 1 final = 7 nodes.
            await _dbContext.Received(7).AddBracketNodeAsync(Arg.Any<BracketNode>(), Arg.Any<CancellationToken>());
            // All 4 round-1 nodes are Fixed (already resolved) -> 4 matches created immediately.
            await _matchModule.Received(4).CreateBracketMatchAsync(
                tournamentId, categoryId, Arg.Any<Guid>(), Arg.Any<Guid>(), "Knockout", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddThirdPlaceRound_WhenEnabledAndSemifinalsExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Knockout", Guid.NewGuid(), 4));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, true, false));

            var pairings = new List<BracketPairingInput> { FixedPairing(), FixedPairing() };
            var command = new GenerateBracketCommand(tournamentId, categoryId, pairings);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert: 2 rounds (semis, final) + 1 third-place round = 3 rounds.
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(3).AddBracketRoundAsync(Arg.Any<BracketRound>(), Arg.Any<CancellationToken>());
            // 2 semifinal + 1 final + 1 third-place = 4 nodes.
            await _dbContext.Received(4).AddBracketNodeAsync(Arg.Any<BracketNode>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldNotCreateMatch_ForGroupPositionPairing_WhenStandingsNotYetAvailable()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "GroupAndKnockout", Guid.NewGuid(), 4));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false));
            _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new List<GroupStandingDto>());

            var pairing = new BracketPairingInput(
                BracketSourceType.GroupPosition, groupId, 1, null,
                BracketSourceType.GroupPosition, groupId, 2, null);
            var command = new GenerateBracketCommand(tournamentId, categoryId, [pairing]);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _matchModule.DidNotReceive().CreateBracketMatchAsync(
                Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldCreateMatch_ForGroupPositionPairing_WhenStandingsAvailable()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var team1 = Guid.NewGuid();
            var team2 = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "GroupAndKnockout", Guid.NewGuid(), 4));
            _tournamentModule.GetTournamentSettingsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentSettingsSummaryDto(tournamentId, 30, 10, 60, 3, 1, 0, false, false));
            _standingModule.GetGroupStandingsAsync(groupId, Arg.Any<CancellationToken>())
                .Returns(new List<GroupStandingDto> { new(team1, 1, 9), new(team2, 2, 6) });

            var pairing = new BracketPairingInput(
                BracketSourceType.GroupPosition, groupId, 1, null,
                BracketSourceType.GroupPosition, groupId, 2, null);
            var command = new GenerateBracketCommand(tournamentId, categoryId, [pairing]);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _matchModule.Received(1).CreateBracketMatchAsync(
                tournamentId, categoryId, team1, team2, "Final", Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenTournamentFormatIsNotKnockout()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Group", Guid.NewGuid(), 4));

            var command = new GenerateBracketCommand(tournamentId, Guid.NewGuid(), [FixedPairing()]);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Format");
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenBracketAlreadyExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            _tournamentModule.GetTournamentAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new TournamentDto(tournamentId, "Cup", "InProgress", "Knockout", Guid.NewGuid(), 4));
            _dbContext.GetBracketByTournamentAndCategoryAsync(tournamentId, categoryId, Arg.Any<CancellationToken>())
                .Returns(Bracket.Create(Guid.NewGuid(), tournamentId, categoryId).Value);

            var command = new GenerateBracketCommand(tournamentId, categoryId, [FixedPairing()]);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Conflict.Bracket");
        }
    }
}
