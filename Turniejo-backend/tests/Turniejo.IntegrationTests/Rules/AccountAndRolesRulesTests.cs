using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.Tournaments.Domain.Enums;
using Turniejo.Users.Application.Users.RegisterUser;

namespace Turniejo.IntegrationTests.Rules;

/// <summary>Arkusz QA „Konto i role”: reguły, które w ręcznym przebiegu wyszły OK.</summary>
public sealed class AccountAndRolesRulesTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static async Task<List<JsonElement>> RolesAsync(HttpClient client) =>
        (await (await client.GetAsync("/api/me/roles")).ReadJsonAsync()).EnumerateArray().ToList();

    private static bool HasRole(List<JsonElement> roles, string role, Guid scopeId) =>
        roles.Any(r => r.GetString("role") == role && r.GetGuid("scopeId") == scopeId);

    [Fact]
    public async Task KON_011_OrganizerCannotCreateASecondOrganization()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, schedule: false);

        var response = await s.Organizer.PostAsJsonAsync("/api/organizations", new { name = "Druga" });

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task KON_012_RefereeCode_GivesRefereeRole_AndRefereeCanBeAssignedToAMatch()
    {
        var s = await TournamentScenario.GroupsAsync(Factory);
        var (referee, refereeId) = await s.JoinAsync("Referee");
        var match = (await s.MatchBetweenAsync("T1", "T2")).GetGuid("id");

        HasRole(await RolesAsync(referee), "Referee", s.TournamentId).Should().BeTrue();
        await s.AssignRefereeAsync(match, refereeId);

        var mine = (await (await referee.GetAsync("/api/referee/matches")).ReadJsonAsync()).GetRawText();
        mine.Should().Contain(match.ToString());
    }

    [Fact]
    public async Task KON_013_CoachAndViewerCodes_GiveMatchingRoles()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, schedule: false);

        var (coach, _) = await s.JoinAsync("Coach");
        var (viewer, _) = await s.JoinAsync("Viewer");

        HasRole(await RolesAsync(coach), "Coach", s.TournamentId).Should().BeTrue();
        HasRole(await RolesAsync(viewer), "Viewer", s.TournamentId).Should().BeTrue();
        HasRole(await RolesAsync(viewer), "Coach", s.TournamentId).Should().BeFalse();
    }

    [Fact]
    public async Task KON_015_UnknownCode_IsRejected()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, schedule: false);
        var (user, _) = await s.NewUserAsync();

        var response = await user.PostAsJsonAsync("/api/tournaments/join", new { code = "NIEMA123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Invalid tournament code");
    }

    [Fact]
    public async Task KON_017a_JoiningTwiceWithTheSameCode_DoesNotDuplicateTheRole()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, schedule: false);
        var (referee, _) = await s.JoinAsync("Referee");

        await s.JoinWithAsync(referee, "Referee");

        (await RolesAsync(referee)).Count(r => r.GetString("role") == "Referee" && r.GetGuid("scopeId") == s.TournamentId)
            .Should().Be(1);
    }

    [Fact]
    public async Task KON_017b_OneAccount_CanBeCoachAndRefereeInTheSameTournament()
    {
        var s = await TournamentScenario.GroupsAsync(Factory, schedule: false);
        var (user, _) = await s.JoinAsync("Coach");

        await s.JoinWithAsync(user, "Referee");

        var roles = await RolesAsync(user);
        HasRole(roles, "Coach", s.TournamentId).Should().BeTrue();
        HasRole(roles, "Referee", s.TournamentId).Should().BeTrue();
    }

    [Fact]
    public async Task KON_021_CoachCannotReadAnotherTeamsSquad()
    {
        var s = await TournamentScenario.GroupsAsync(Factory);
        var (coach, coachId) = await s.JoinAsync("Coach");
        await s.AssignCoachAsync("T1", coachId);

        (await coach.GetAsync($"/api/coach/teams/{s.Team("T1")}/players")).IsSuccessStatusCode.Should().BeTrue();
        (await coach.GetAsync($"/api/coach/teams/{s.Team("T2")}/players")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task KON_022_ParentCannotReadSomeoneElsesChild()
    {
        var s = await TournamentScenario.GroupsAsync(Factory);
        var (_, ownChild) = await s.CreatePlayerAsync("T1", "Ola", "Własna");
        var (_, otherChild) = await s.CreatePlayerAsync("T2", "Ela", "Obca");
        var (parent, parentId) = await s.NewUserAsync("parent");
        await s.AddGuardianAsync(ownChild, "T1", parentId);

        (await parent.GetAsync($"/api/parent/players/{ownChild}/team")).IsSuccessStatusCode.Should().BeTrue();
        (await parent.GetAsync($"/api/parent/players/{otherChild}/team")).IsSuccessStatusCode.Should().BeFalse();
        (await parent.GetAsync($"/api/players/{otherChild}")).IsSuccessStatusCode.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/settings")]
    [InlineData("/members")]
    [InlineData("/dashboard")]
    [InlineData("/teams")]
    public async Task KON_023_KON_026_OtherOrganizer_CannotReadManagementData(string path)
    {
        var a = await TournamentScenario.GroupsAsync(Factory, schedule: false);
        var b = await TournamentScenario.GroupsAsync(Factory, schedule: false);

        var response = await b.Organizer.GetAsync($"/api/tournaments/{a.TournamentId}{path}");

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task KON_024_OnlyACancelledTournament_CanBeDeleted_AndItsPublicPageDisappears()
    {
        var s = await TournamentScenario.GroupsAsync(Factory);
        var slug = (await s.TournamentAsync()).GetString("slug");
        (await s.Organizer.PutAsJsonAsync($"/api/tournaments/{s.TournamentId}/public-settings",
                new { isPublic = true, showTeams = true, showPlayers = true, showSchedule = true, showStandings = true, showBracket = true, showLiveMatches = true, showStats = true }))
            .IsSuccessStatusCode.Should().BeTrue();
        var anonymous = CreateClient();
        (await anonymous.GetAsync($"/public/tournaments/{slug}")).IsSuccessStatusCode.Should().BeTrue();

        (await s.Organizer.DeleteAsync($"/api/tournaments/{s.TournamentId}")).IsSuccessStatusCode.Should().BeFalse();

        await TournamentFlow.ChangeStatusAsync(s.Organizer, s.TournamentId, TournamentStatus.Cancelled);
        (await s.Organizer.DeleteAsync($"/api/tournaments/{s.TournamentId}")).IsSuccessStatusCode.Should().BeTrue();

        (await anonymous.GetAsync($"/public/tournaments/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var list = (await (await s.Organizer.GetAsync("/api/tournaments")).ReadJsonAsync()).GetRawText();
        list.Should().NotContain(s.TournamentId.ToString());
    }

    [Fact]
    public async Task KON_038a_PasswordOfNineCharacters_IsRejected()
    {
        var response = await CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterUserCommand($"short-{Guid.NewGuid():N}@integration.test", "Abcdefgh1", "Jan", "Krótki", AcceptTerms: true));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task KON_043_OrganizerWithoutOrganizationName_GetsOneNamedAfterThem()
    {
        var anonymous = CreateClient();
        var email = $"jan-{Guid.NewGuid():N}@integration.test";
        var register = await anonymous.PostAsJsonAsync("/api/auth/register",
            new RegisterUserCommand(email, "Abcdefghi1", "Jan", "Testowy", AcceptTerms: true, CreateOrganization: true, OrganizationName: null));
        register.IsSuccessStatusCode.Should().BeTrue(await register.Content.ReadAsStringAsync());
        var userId = (await register.ReadJsonAsync()).GetGuid("userId");
        await FlushOutboxAsync();

        var organizer = CreateAuthenticatedClient(userId, email);
        var organization = await (await organizer.GetAsync("/api/organizations/mine")).ReadJsonAsync();

        organization.GetString("name").Should().Be("Jan Testowy");
    }
}
