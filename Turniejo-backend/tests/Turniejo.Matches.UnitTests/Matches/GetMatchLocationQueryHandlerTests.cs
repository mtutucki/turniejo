using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.GetMatchLocation;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Exceptions;

namespace Turniejo.Matches.UnitTests.Matches
{
    public sealed class GetMatchLocationQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IAuthorizationService _authorizationService;
        private readonly IPlayerModule _playerModule;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly GetMatchLocationQueryHandler _handler;

        public GetMatchLocationQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _authorizationService = Substitute.For<IAuthorizationService>();
            _playerModule = Substitute.For<IPlayerModule>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            var accessGuard = new MatchAccessGuard(_authorizationService, _playerModule);
            _handler = new GetMatchLocationQueryHandler(_dbContext, accessGuard, _currentUserContext);
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenMatchNotFound()
        {
            // Arrange
            var matchId = Guid.NewGuid();
            _dbContext.GetMatchByIdAsync(matchId, Arg.Any<CancellationToken>()).Returns((Match?)null);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(matchId), CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Match.NotFound");
        }

        [Fact]
        public async Task Handle_ShouldReturnPitchDetails_WhenUserIsTournamentMember()
        {
            // Arrange
            var pitchId = Guid.NewGuid();
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), pitchId, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _authorizationService.HasAnyRoleAsync(
                _currentUserContext.UserId, "Tournament", match.TournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            var pitch = Pitch.Create(pitchId, match.TournamentId, "Boisko 1", "Obok wejścia głównego", Guid.NewGuid()).Value;
            _dbContext.GetPitchByIdAsync(pitchId, Arg.Any<CancellationToken>()).Returns(pitch);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PitchName.Should().Be("Boisko 1");
        }

        [Fact]
        public async Task Handle_ShouldReturnNullPitchInfo_WhenNoPitchAssigned()
        {
            // Arrange
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _authorizationService.HasAnyRoleAsync(
                _currentUserContext.UserId, "Tournament", match.TournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.PitchId.Should().BeNull();
            result.Value.PitchName.Should().BeNull();
        }

        [Fact]
        public async Task Handle_ShouldReturnPitchDetails_WhenUserIsGuardianOfAPlayerInTheMatch()
        {
            // Domyka lukę z audytu: rodzic zawodnika grającego w meczu (bez roli turniejowej) też
            // powinien widzieć lokalizację boiska.
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _authorizationService.HasAnyRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _playerModule.IsGuardianOfAnyPlayerOnTeamsAsync(
                _currentUserContext.UserId,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(match.HomeTournamentTeamId) && ids.Contains(match.AwayTournamentTeamId)),
                Arg.Any<CancellationToken>())
                .Returns(true);

            // Act
            var result = await _handler.Handle(new GetMatchLocationQuery(match.Id), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task Handle_ShouldThrowUnauthorized_WhenUserIsNeitherTournamentMemberNorGuardian()
        {
            // Zamyka IDOR z audytu: dowolny zalogowany user widział lokalizację dowolnego meczu.
            var match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            _dbContext.GetMatchByIdAsync(match.Id, Arg.Any<CancellationToken>()).Returns(match);
            _authorizationService.HasAnyRoleAsync(
                Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);
            _playerModule.IsGuardianOfAnyPlayerOnTeamsAsync(
                Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
                .Returns(false);

            // Act
            var act = () => _handler.Handle(new GetMatchLocationQuery(match.Id), CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedException>();
        }
    }
}
