using MediatR;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Standings.Application.EventHandlers;
using Turniejo.Standings.Application.Standings.RecalculateStandings;
using Turniejo.Tournaments.Domain.Events;

namespace Turniejo.Standings.UnitTests.EventHandlers
{
    // QA v3: GRP-017 - zmiana kolejności kryteriów zapisywała się, ale tabela przeliczała się dopiero
    // po kolejnym wyniku, więc przez ten czas była niezgodna z ustawionymi regułami.
    public sealed class TieBreakerRulesSetHandlerTests
    {
        [Fact]
        public async Task GRP_017_ChangingTieBreakerRules_RecalculatesEveryGroup()
        {
            var tournamentModule = Substitute.For<ITournamentModule>();
            var sender = Substitute.For<ISender>();
            var tournamentId = Guid.NewGuid();
            var (groupA, groupB) = (Guid.NewGuid(), Guid.NewGuid());
            tournamentModule.GetTournamentGroupsAsync(tournamentId, Arg.Any<CancellationToken>())
                .Returns([new TournamentGroupDto(groupA, tournamentId, Guid.NewGuid(), "A"), new TournamentGroupDto(groupB, tournamentId, Guid.NewGuid(), "B")]);
            var handler = new TieBreakerRulesSetHandler(tournamentModule, sender);

            await handler.Handle(new TieBreakerRulesSet(tournamentId, Guid.NewGuid(), Guid.NewGuid(), []), CancellationToken.None);

            await sender.Received(1).Send(Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupA), Arg.Any<CancellationToken>());
            await sender.Received(1).Send(Arg.Is<RecalculateStandingsCommand>(c => c.GroupId == groupB), Arg.Any<CancellationToken>());
        }
    }
}
