using FluentAssertions;
using NSubstitute;
using Turniejo.Matches.Domain.Events;
using Turniejo.Notifications.Application.Abstractions;
using Turniejo.Notifications.Application.EventHandlers;
using Turniejo.Notifications.Application.Services;
using Turniejo.SharedKernel.Contracts;
using NotificationEntity = Turniejo.Notifications.Domain.Entities.Notification;

namespace Turniejo.Notifications.UnitTests.EventHandlers;

// QA v3: PAN-008, PUC-032 - powiadomienia podawały godzinę w UTC (o godzinę za wcześnie zimą).
public sealed class NotificationLocalTimeTests
{
    private readonly INotificationsDbContext _dbContext = Substitute.For<INotificationsDbContext>();
    private readonly IMatchModule _matchModule = Substitute.For<IMatchModule>();
    private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
    private readonly IPlayerModule _playerModule = Substitute.For<IPlayerModule>();
    private readonly List<NotificationEntity> _published = [];
    private readonly MatchInfoDto _match;

    // 14 listopada 08:00 UTC = 09:00 w Polsce (czas zimowy).
    private static readonly DateTimeOffset WinterUtc = new(2026, 11, 14, 8, 0, 0, TimeSpan.Zero);

    public NotificationLocalTimeTests()
    {
        _playerModule.GetTournamentTeamPlayersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _teamModule.GetTeamStaffUserIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(new List<Guid>());
        _dbContext.AddNotificationAsync(Arg.Do<NotificationEntity>(n => _published.Add(n)), Arg.Any<CancellationToken>());

        _match = new MatchInfoDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, WinterUtc, "Scheduled");
        _matchModule.GetMatchAsync(_match.Id, Arg.Any<CancellationToken>()).Returns(_match);
    }

    private NotificationPublisher Publisher => new(_dbContext);
    private MatchNotificationRecipientResolver Resolver => new(_teamModule, _playerModule);

    [Fact]
    public async Task PUC_032_MatchTimeChanged_ShowsPolishLocalTime()
    {
        var handler = new MatchTimeChangedHandler(Publisher, Resolver, _matchModule, _dbContext);

        await handler.Handle(new MatchTimeChanged(_match.Id, _match.TournamentId, WinterUtc, Guid.NewGuid()), CancellationToken.None);

        _published.Should().ContainSingle().Which.Message.Should().Contain("09:00").And.NotContain("08:00");
    }

    [Fact]
    public async Task PAN_008_MatchScheduleChanged_ShowsPolishLocalTime()
    {
        var handler = new MatchScheduleChangedHandler(Publisher, Resolver, _matchModule, _dbContext);

        await handler.Handle(new MatchScheduleChanged(_match.Id, _match.TournamentId, Guid.NewGuid(), WinterUtc, Guid.NewGuid()), CancellationToken.None);

        _published.Should().ContainSingle().Which.Message.Should().Contain("09:00").And.NotContain("08:00");
    }

    [Fact]
    public async Task PAN_010_Reminder_ShowsPolishLocalTime()
    {
        _matchModule.GetMatchesScheduledBetweenAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns([_match]);
        _teamModule.GetTournamentTeamAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((TournamentTeamDto?)null);
        var handler = new Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications.CreateMatchReminderNotificationsCommandHandler(
            _dbContext, Publisher, Resolver, _matchModule, _teamModule);

        await handler.Handle(new Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications.CreateMatchReminderNotificationsCommand(30), CancellationToken.None);

        _published.Should().ContainSingle().Which.Message.Should().Contain("o 09:00");
    }

    [Fact]
    public async Task PUC_032_SummerTime_UsesTwoHourOffset()
    {
        var handler = new MatchTimeChangedHandler(Publisher, Resolver, _matchModule, _dbContext);
        var summerUtc = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero);

        await handler.Handle(new MatchTimeChanged(_match.Id, _match.TournamentId, summerUtc, Guid.NewGuid()), CancellationToken.None);

        _published.Should().ContainSingle().Which.Message.Should().Contain("10:00");
    }
}
