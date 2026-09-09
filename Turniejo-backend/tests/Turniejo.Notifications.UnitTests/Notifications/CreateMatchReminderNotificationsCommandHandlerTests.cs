using FluentAssertions;
using NSubstitute;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Notifications.UnitTests.Notifications;

public sealed class CreateMatchReminderNotificationsCommandHandlerTests
{
    private readonly INotificationsDbContext _dbContext;
    private readonly IMatchModule _matchModule;
    private readonly ITeamModule _teamModule;
    private readonly IPlayerModule _playerModule;
    private readonly CreateMatchReminderNotificationsCommandHandler _handler;

    public CreateMatchReminderNotificationsCommandHandlerTests()
    {
        _dbContext = Substitute.For<INotificationsDbContext>();
        _matchModule = Substitute.For<IMatchModule>();
        _teamModule = Substitute.For<ITeamModule>();
        _playerModule = Substitute.For<IPlayerModule>();
        _playerModule.GetTournamentTeamPlayersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);

        var publisher = new NotificationPublisher(_dbContext);
        var resolver = new MatchNotificationRecipientResolver(_teamModule, _playerModule);

        _handler = new CreateMatchReminderNotificationsCommandHandler(_dbContext, publisher, resolver, _matchModule, _teamModule);

        _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
    }

    private static MatchInfoDto CreateMatch(Guid? refereeId = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), refereeId, null, DateTimeOffset.UtcNow.AddMinutes(20), "Scheduled");

    [Fact]
    public async Task Handle_ShouldCreateReminder_WhenMatchHasRecipientsAndNoExistingReminder()
    {
        // Arrange
        var match = CreateMatch(refereeId: Guid.NewGuid());
        _matchModule.GetMatchesScheduledBetweenAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        _dbContext.MatchReminderExistsAsync(match.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new CreateMatchReminderNotificationsCommand(30), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(1);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipMatch_WhenReminderAlreadyExists()
    {
        // Arrange
        var match = CreateMatch(refereeId: Guid.NewGuid());
        _matchModule.GetMatchesScheduledBetweenAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        _dbContext.MatchReminderExistsAsync(match.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _handler.Handle(new CreateMatchReminderNotificationsCommand(30), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(0);
        await _dbContext.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipMatch_WhenNoRecipientsResolved()
    {
        // Arrange
        var match = CreateMatch(refereeId: null);
        _matchModule.GetMatchesScheduledBetweenAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        _dbContext.MatchReminderExistsAsync(match.Id, Arg.Any<CancellationToken>()).Returns(false);
        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());

        // Act
        var result = await _handler.Handle(new CreateMatchReminderNotificationsCommand(30), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldAggregateCreatedCount_AcrossMultipleQualifyingMatches()
    {
        // Zamyka lukę z audytu testów: handler był testowany tylko dla pojedynczego meczu na
        // wywołanie — nikt nie sprawdzał, że createdCount faktycznie sumuje się po całej paczce,
        // ani że SaveChangesAsync leci raz na koniec, nie raz na mecz.
        var matchA = CreateMatch(refereeId: Guid.NewGuid());
        var matchB = CreateMatch(refereeId: Guid.NewGuid());
        _matchModule.GetMatchesScheduledBetweenAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([matchA, matchB]);
        _dbContext.MatchReminderExistsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new CreateMatchReminderNotificationsCommand(30), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CreatedCount.Should().Be(2);
        await _dbContext.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _dbContext.Received(2).AddNotificationAsync(Arg.Any<Turniejo.Notifications.Domain.Entities.Notification>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFallBackToUnknown_WhenTeamLookupMisses()
    {
        // _teamModule.GetTournamentTeamAsync jest w konstruktorze zamockowane na null dla każdego
        // ID — to właśnie ścieżka fallbacku na "Unknown" nazwę drużyny w treści przypomnienia.
        var match = CreateMatch(refereeId: Guid.NewGuid());
        _matchModule.GetMatchesScheduledBetweenAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([match]);
        _dbContext.MatchReminderExistsAsync(match.Id, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _handler.Handle(new CreateMatchReminderNotificationsCommand(30), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _dbContext.Received(1).AddNotificationAsync(
            Arg.Is<Turniejo.Notifications.Domain.Entities.Notification>(n => n.Message.Contains("Unknown")),
            Arg.Any<CancellationToken>());
    }
}
