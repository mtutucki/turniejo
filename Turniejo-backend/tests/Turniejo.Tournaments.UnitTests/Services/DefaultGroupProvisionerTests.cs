using FluentAssertions;
using NSubstitute;
using Turniejo.SharedKernel.Contracts;
using Turniejo.Tournaments.Application.Abstractions;
using Turniejo.Tournaments.Application.Services;
using Turniejo.Tournaments.Domain.Entities;
using Turniejo.Tournaments.Domain.Enums;

namespace Turniejo.Tournaments.UnitTests.Services;

public sealed class DefaultGroupProvisionerTests
{
    private readonly ITournamentsDbContext _dbContext = Substitute.For<ITournamentsDbContext>();
    private readonly ITeamModule _teamModule = Substitute.For<ITeamModule>();
    private readonly ICurrentUserContext _currentUserContext = Substitute.For<ICurrentUserContext>();
    private readonly DefaultGroupProvisioner _provisioner;

    public DefaultGroupProvisionerTests()
    {
        _provisioner = new DefaultGroupProvisioner(_dbContext, _teamModule, _currentUserContext);
        _currentUserContext.UserId.Returns(Guid.NewGuid());
    }

    private static Tournament TournamentWith(TournamentFormat? format)
    {
        var tournament = Tournament.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Turniej",
            DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2), 8, null).Value;
        if (format.HasValue)
            tournament.SetFormat(format.Value);
        tournament.ClearDomainEvents();
        return tournament;
    }

    private void CategoryHasGroups(Guid categoryId, int count) =>
        _dbContext.GetTournamentGroupsByCategoryIdAsync(categoryId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, count)
                .Select(i => TournamentGroup.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), categoryId, $"G{i}", i + 1).Value)
                .ToList());

    [Theory]
    [InlineData(TournamentFormat.Group)]
    [InlineData(TournamentFormat.GroupAndKnockout)]
    public async Task EnsureDefaultGroups_ShouldCreateGroupA_AndAssignTeams_WhenCategoryHasNoGroups(TournamentFormat format)
    {
        var tournament = TournamentWith(format);
        var categoryId = Guid.NewGuid();
        CategoryHasGroups(categoryId, 0);

        await _provisioner.EnsureDefaultGroupsAsync(tournament, [categoryId]);

        await _dbContext.Received(1).AddTournamentGroupAsync(
            Arg.Is<TournamentGroup>(g => g.CategoryId == categoryId && g.Name == "Grupa A" && g.TournamentId == tournament.Id),
            Arg.Any<CancellationToken>());
        await _teamModule.Received(1).AssignUngroupedTeamsToGroupAsync(
            tournament.Id, categoryId, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureDefaultGroups_ShouldLeaveExistingGroupsAlone()
    {
        var tournament = TournamentWith(TournamentFormat.Group);
        var categoryId = Guid.NewGuid();
        CategoryHasGroups(categoryId, 2);

        await _provisioner.EnsureDefaultGroupsAsync(tournament, [categoryId]);

        await _dbContext.DidNotReceive().AddTournamentGroupAsync(Arg.Any<TournamentGroup>(), Arg.Any<CancellationToken>());
        await _teamModule.DidNotReceive().AssignUngroupedTeamsToGroupAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(TournamentFormat.Knockout)]
    [InlineData(null)]
    public async Task EnsureDefaultGroups_ShouldDoNothing_WithoutGroupStage(TournamentFormat? format)
    {
        var tournament = TournamentWith(format);
        var categoryId = Guid.NewGuid();
        CategoryHasGroups(categoryId, 0);

        await _provisioner.EnsureDefaultGroupsAsync(tournament, [categoryId]);

        await _dbContext.DidNotReceive().AddTournamentGroupAsync(Arg.Any<TournamentGroup>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureDefaultGroups_ShouldDoNothing_AfterScheduleGenerated()
    {
        var tournament = TournamentWith(TournamentFormat.Group);
        var userId = Guid.NewGuid();
        tournament.ChangeStatus(TournamentStatus.RegistrationOpen, userId);
        tournament.ChangeStatus(TournamentStatus.RegistrationClosed, userId);
        tournament.ChangeStatus(TournamentStatus.ScheduleGenerated, userId).IsSuccess.Should().BeTrue();
        var categoryId = Guid.NewGuid();
        CategoryHasGroups(categoryId, 0);

        await _provisioner.EnsureDefaultGroupsAsync(tournament, [categoryId]);

        await _dbContext.DidNotReceive().AddTournamentGroupAsync(Arg.Any<TournamentGroup>(), Arg.Any<CancellationToken>());
    }
}
