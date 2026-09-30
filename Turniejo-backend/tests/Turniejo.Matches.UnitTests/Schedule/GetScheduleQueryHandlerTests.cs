using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Application.Schedule.GetSchedule;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Matches.UnitTests.Schedule
{
    public sealed class GetScheduleQueryHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly IAuthorizationService _authorizationService = Substitute.For<IAuthorizationService>();
        private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
        private readonly GetScheduleQueryHandler _handler;

        public GetScheduleQueryHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _handler = new GetScheduleQueryHandler(_dbContext, new RefereeIdVisibility(_authorizationService, _currentUserContext));
            // domyślnie organizator — widzi RefereeId
            _authorizationService.HasAnyRoleAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        [Fact]
        public async Task Handle_ShouldReturnMappedDtos_OrderedByScheduledAt()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            var later = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(2), StageType.Group).Value;
            var earlier = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;

            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match> { later, earlier });

            // Act
            var result = await _handler.Handle(new GetScheduleQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().HaveCount(2);
            result.Value[0].Id.Should().Be(earlier.Id);
            result.Value[1].Id.Should().Be(later.Id);
        }

        [Fact]
        public async Task Handle_ShouldReturnEmptyList_WhenNoMatchesExist()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns(new List<Match>());

            // Act
            var result = await _handler.Handle(new GetScheduleQuery(tournamentId), CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEmpty();
        }
        [Fact]
        public async Task Handle_ShouldHideOtherRefereeIds_FromNonOrganizers()
        {
            var tournamentId = Guid.NewGuid();
            var me = Guid.NewGuid();
            var mine = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(1), StageType.Group).Value;
            mine.AssignReferee(me, Guid.NewGuid());
            var someoneElses = Match.Create(Guid.NewGuid(), tournamentId, null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow.AddDays(2), StageType.Group).Value;
            someoneElses.AssignReferee(Guid.NewGuid(), Guid.NewGuid());
            _dbContext.GetMatchesByTournamentIdAsync(tournamentId, Arg.Any<CancellationToken>()).Returns(new List<Match> { mine, someoneElses });
            _currentUserContext.UserId.Returns(me);
            _authorizationService.HasAnyRoleAsync(me, "Tournament", tournamentId, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
                .Returns(false);

            var result = await _handler.Handle(new GetScheduleQuery(tournamentId), CancellationToken.None);

            result.Value[0].RefereeId.Should().Be(me);
            result.Value[1].RefereeId.Should().BeNull();
        }
    }
}
