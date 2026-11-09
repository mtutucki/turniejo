using FluentAssertions;
using MediatR;
using NSubstitute;
using Turniejo.Matches.Application.Abstractions;
using Turniejo.Matches.Application.Matches.AddCard;
using Turniejo.Matches.Application.Matches.RecalculateMatchScore;
using Turniejo.Matches.Application.Matches.RevertMatchEvent;
using Turniejo.Matches.Application.Services;
using Turniejo.Matches.Domain.Entities;
using Turniejo.Matches.Domain.Enums;
using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Matches.UnitTests.Matches
{
    // QA v3: MNZ-015 - po cofnięciu pierwszej z dwóch żółtych automatyczna czerwona zostawała i zawodnik
    // dalej był wykluczony. Decyzja: cofnięcie żółtej cofa też czerwoną z drugiej żółtej.
    public sealed class SecondYellowRevertTests
    {
        private readonly IMatchesDbContext _dbContext = Substitute.For<IMatchesDbContext>();
        private readonly ISender _sender = Substitute.For<ISender>();
        private readonly ICurrentUserContext _user = Substitute.For<ICurrentUserContext>();
        private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
        private readonly Match _match;
        private readonly Guid _player = Guid.NewGuid();

        public SecondYellowRevertTests()
        {
            _user.UserId.Returns(Guid.NewGuid());
            _sender.Send(Arg.Any<RecalculateMatchScoreCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Success(new RecalculateMatchScoreResponse(0, 0)));
            _match = Match.Create(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), Guid.NewGuid(), null, DateTimeOffset.UtcNow, StageType.Group).Value;
            _match.Start(Guid.NewGuid());
            _match.ClearDomainEvents();
            _dbContext.GetMatchByIdAsync(_match.Id, Arg.Any<CancellationToken>()).Returns(_match);
            _dbContext.GetMatchEventsByMatchIdAsync(_match.Id, Arg.Any<CancellationToken>()).Returns(new List<MatchEvent>());
            _playerModule.GetTournamentTeamPlayersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
                .Returns(call => [new TournamentTeamPlayerDto(Guid.NewGuid(), call.Arg<Guid>(), _player, "Jan", "Kowalski", 7, null, "Active")]);
        }

        private MatchEvent Card(MatchEventType type, int minute, bool secondYellow = false)
        {
            var card = MatchEvent.Create(Guid.NewGuid(), _match.Id, type, minute, Guid.NewGuid(), _match.HomeTournamentTeamId, _player, isSecondYellow: secondYellow).Value;
            _dbContext.GetMatchEventByIdAsync(card.Id, Arg.Any<CancellationToken>()).Returns(card);
            return card;
        }

        [Fact]
        public async Task SecondYellow_IsStoredAsRedCard_MarkedAsSecondYellow()
        {
            var firstYellow = Card(MatchEventType.YellowCard, 10);
            _dbContext.GetActiveMatchEventsByTypeAsync(_match.Id, MatchEventType.YellowCard, Arg.Any<CancellationToken>()).Returns([firstYellow]);
            _dbContext.GetActiveMatchEventsByTypeAsync(_match.Id, MatchEventType.RedCard, Arg.Any<CancellationToken>()).Returns(new List<MatchEvent>());
            MatchEvent? stored = null;
            await _dbContext.AddMatchEventAsync(Arg.Do<MatchEvent>(e => stored = e), Arg.Any<CancellationToken>());
            var handler = new AddCardCommandHandler(_dbContext, new MatchEventRosterGuard(_playerModule), new SendOffRules(_dbContext), _user);

            var result = await handler.Handle(
                new AddCardCommand(_match.TournamentId, _match.Id, _match.HomeTournamentTeamId, _player, MatchEventType.YellowCard, 30, null),
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            stored!.Type.Should().Be(MatchEventType.RedCard);
            stored.IsSecondYellow.Should().BeTrue();
        }

        [Fact]
        public async Task MNZ_015_RevertingFirstYellow_TurnsTheSecondYellowRedBackIntoAYellow()
        {
            var firstYellow = Card(MatchEventType.YellowCard, 10);
            var autoRed = Card(MatchEventType.RedCard, 30, secondYellow: true);
            _dbContext.GetActiveMatchEventsByTypeAsync(_match.Id, MatchEventType.RedCard, Arg.Any<CancellationToken>()).Returns([autoRed]);
            var handler = new RevertMatchEventCommandHandler(_dbContext, _sender, _user);

            var result = await handler.Handle(new RevertMatchEventCommand(_match.TournamentId, _match.Id, firstYellow.Id), CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            firstYellow.IsDeleted.Should().BeTrue();
            autoRed.IsDeleted.Should().BeFalse();
            autoRed.Type.Should().Be(MatchEventType.YellowCard);
            autoRed.IsSecondYellow.Should().BeFalse();
        }

        [Fact]
        public async Task RevertingFirstYellow_LeavesADirectRedCardAlone()
        {
            var firstYellow = Card(MatchEventType.YellowCard, 10);
            var directRed = Card(MatchEventType.RedCard, 30);
            _dbContext.GetActiveMatchEventsByTypeAsync(_match.Id, MatchEventType.RedCard, Arg.Any<CancellationToken>()).Returns([directRed]);
            var handler = new RevertMatchEventCommandHandler(_dbContext, _sender, _user);

            await handler.Handle(new RevertMatchEventCommand(_match.TournamentId, _match.Id, firstYellow.Id), CancellationToken.None);

            directRed.Type.Should().Be(MatchEventType.RedCard);
        }
    }
}
