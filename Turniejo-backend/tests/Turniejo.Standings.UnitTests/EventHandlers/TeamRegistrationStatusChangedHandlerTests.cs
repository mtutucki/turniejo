using MediatR;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.Abstractions;
using Turniejo.Standings.Application.EventHandlers;
using Turniejo.Standings.Application.Standings.RecalculateStandings;
using Turniejo.Standings.Domain.Entities;
using Turniejo.Teams.Domain.Enums;
using Turniejo.Teams.Domain.Events;

namespace Turniejo.Standings.UnitTests.EventHandlers
{
    public sealed class TeamRegistrationStatusChangedHandlerTests
    {
        private readonly IStandingsDbContext _dbContext;
        private readonly ITeamModule _teamModule;
        private readonly ISender _sender;
        private readonly TeamRegistrationStatusChangedHandler _handler;

        public TeamRegistrationStatusChangedHandlerTests()
        {
            _dbContext = Substitute.For<IStandingsDbContext>();
            _teamModule = Substitute.For<ITeamModule>();
            _sender = Substitute.For<ISender>();
            _handler = new TeamRegistrationStatusChangedHandler(_dbContext, _teamModule, _sender);
        }

        [Fact]
        public async Task Handle_ShouldRemoveStaleStandingAndTriggerRecalculation_WhenTeamWithdraws()
        {
            // Zamyka lukę z audytu: wycofanie drużyny zostawiało jej Standing w tabeli na zawsze.
            var teamId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var standing = Standing.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), groupId, teamId).Value;
            _teamModule.GetTournamentTeamAsync(teamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(teamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), groupId, "Withdrawn"));
            _dbContext.GetStandingAsync(groupId, teamId, Arg.Any<CancellationToken>()).Returns(standing);
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.Received(1).RemoveStandingAsync(standing, Arg.Any<CancellationToken>());
            await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
            await _sender.Received(1).Send(
                Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupId),
                Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldDoNothing_WhenStatusIsNotWithdrawn()
        {
            var teamId = Guid.NewGuid();
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Pending, RegistrationStatus.Accepted, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _teamModule.DidNotReceive().GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _sender.DidNotReceive().Send(Arg.Any<RecalculateStandingsCommand>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldSkipRemoval_WhenTeamHasNoGroup()
        {
            var teamId = Guid.NewGuid();
            _teamModule.GetTournamentTeamAsync(teamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(teamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Withdrawn"));
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().RemoveStandingAsync(Arg.Any<Standing>(), Arg.Any<CancellationToken>());
            await _sender.DidNotReceive().Send(Arg.Any<RecalculateStandingsCommand>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ShouldStillTriggerRecalculation_WhenNoStandingRowExistedYet()
        {
            // Drużyna mogła się wycofać zanim jakikolwiek mecz w grupie się odbył — bez wiersza do
            // usunięcia, ale reszta grupy nadal powinna dostać odświeżoną tabelę.
            var teamId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            _teamModule.GetTournamentTeamAsync(teamId, Arg.Any<CancellationToken>())
                .Returns(new TournamentTeamDto(teamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), groupId, "Withdrawn"));
            _dbContext.GetStandingAsync(groupId, teamId, Arg.Any<CancellationToken>()).Returns((Standing?)null);
            var notification = new TeamRegistrationStatusChanged(teamId, Guid.NewGuid(), RegistrationStatus.Accepted, RegistrationStatus.Withdrawn, Guid.NewGuid());

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            await _dbContext.DidNotReceive().RemoveStandingAsync(Arg.Any<Standing>(), Arg.Any<CancellationToken>());
            await _sender.Received(1).Send(
                Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupId),
                Arg.Any<CancellationToken>());
        }
    }
}
