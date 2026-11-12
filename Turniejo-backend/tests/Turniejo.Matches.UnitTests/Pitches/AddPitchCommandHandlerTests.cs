using Turniejo.Matches.Application.Services;
using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Pitches.AddPitch;
using Turniejo.Matches.Domain.Entities;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Outbox;

namespace Turniejo.Matches.UnitTests.Pitches
{
    public sealed class AddPitchCommandHandlerTests
    {
        private readonly IMatchesDbContext _dbContext;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ITournamentModule _tournamentModule;
        private readonly AddPitchCommandHandler _handler;

        public AddPitchCommandHandlerTests()
        {
            _dbContext = Substitute.For<IMatchesDbContext>();
            _currentUserContext = Substitute.For<ICurrentUserContext>();
            _tournamentModule = Substitute.For<ITournamentModule>();
            _handler = new AddPitchCommandHandler(_dbContext, _currentUserContext, _tournamentModule, Substitute.For<IAuthorizationService>());
        }

        [Fact]
        public async Task Handle_ShouldSucceed_WhenDataIsValid()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddPitchCommand(Guid.NewGuid(), "Boisko 1", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddPitchAsync(Arg.Any<Pitch>(), Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSaveGroups_WhenTheyBelongToTheTournament()
        {
            var tournamentId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns([new TournamentGroupDto(groupId, tournamentId, Guid.NewGuid(), "Grupa A")]);

            var result = await _handler.Handle(new AddPitchCommand(tournamentId, "Boisko A", null, [groupId]), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            await _dbContext.Received(1).AddPitchAsync(
                Arg.Is<Pitch>(p => p.GroupIds.SequenceEqual(new[] { groupId })), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenGroupIsFromAnotherTournament()
        {
            var tournamentId = Guid.NewGuid();
            _tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns([new TournamentGroupDto(Guid.NewGuid(), tournamentId, Guid.NewGuid(), "Grupa A")]);

            var result = await _handler.Handle(new AddPitchCommand(tournamentId, "Boisko A", null, [Guid.NewGuid()]), CancellationToken.None);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.GroupIds");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldFail_WhenNameAlreadyExists()
        {
            // Arrange
            var tournamentId = Guid.NewGuid();
            _dbContext.PitchNameExistsAsync(tournamentId, "Boisko 1", Arg.Any<CancellationToken>()).Returns(true);
            var command = new AddPitchCommand(tournamentId, "Boisko 1", null);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("Validation.Name");
            await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldAddOutboxMessage_WhenSuccessful()
        {
            // Arrange
            _currentUserContext.UserId.Returns(Guid.NewGuid());
            var command = new AddPitchCommand(Guid.NewGuid(), "Boisko 1", null);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).AddOutboxMessageAsync(Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>());
        }
    }
}
