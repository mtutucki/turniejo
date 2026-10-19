using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Turniejo.IntegrationTests.Infrastructure;
using Turniejo.SharedKernel.Results;
using Turniejo.Users.Application.Users.ConfirmPasswordReset;
using Turniejo.Users.Application.Users.RequestPasswordReset;

namespace Turniejo.IntegrationTests.Scenarios;

// Komendy idą przez ISender, nie przez HTTP — endpointy /api/auth/* dzielą limiter AuthAnonymous
// (5 żądań / 5 min na cały przebieg testów), który RegistrationHttpFlowTests już prawie wyczerpuje.
public sealed class PasswordResetTests(IntegrationTestWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Email = "reset@integration.test";
    private const string LinkMarker = "/password-reset/confirm?token=";

    private async Task<Result> SendAsync(IRequest<Result> command)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
    }

    private async Task<string> RequestTokenAsync()
    {
        Factory.FakeEmail.Reset();
        (await SendAsync(new RequestPasswordResetCommand(Email))).IsSuccess.Should().BeTrue();

        var mail = Factory.FakeEmail.Sent.Should().ContainSingle().Subject;
        mail.To.Should().Be(Email);
        return mail.TextBody[(mail.TextBody.IndexOf(LinkMarker, StringComparison.Ordinal) + LinkMarker.Length)..]
            .Split('\r', '\n')[0];
    }

    [Fact]
    public async Task ResetLink_SetsNewPassword_AndWorksOnlyOnce()
    {
        var userId = await TournamentFlow.SeedUserAsync(Factory, Email);
        var token = await RequestTokenAsync();

        (await SendAsync(new ConfirmPasswordResetCommand(token, "NoweHaslo123"))).IsSuccess.Should().BeTrue();

        Factory.FakeKeycloak.PasswordChanges.Should().ContainSingle().Which.Should().Be((userId, "NoweHaslo123"));
        Factory.FakeKeycloak.LoggedOutUsers.Should().Contain(userId);

        var secondUse = await SendAsync(new ConfirmPasswordResetCommand(token, "InneHaslo456"));
        secondUse.IsFailure.Should().BeTrue();
        secondUse.Error.Code.Should().Be("Validation.Token");
        Factory.FakeKeycloak.PasswordChanges.Should().ContainSingle();
    }

    [Fact]
    public async Task NewerLink_InvalidatesThePreviousOne()
    {
        await TournamentFlow.SeedUserAsync(Factory, Email);
        var firstToken = await RequestTokenAsync();
        var secondToken = await RequestTokenAsync();

        (await SendAsync(new ConfirmPasswordResetCommand(firstToken, "NoweHaslo123"))).IsFailure.Should().BeTrue();
        (await SendAsync(new ConfirmPasswordResetCommand(secondToken, "NoweHaslo123"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Request_SendsNothing_WhenEmailIsUnknown()
    {
        Factory.FakeEmail.Reset();

        (await SendAsync(new RequestPasswordResetCommand("nikt@integration.test"))).IsSuccess.Should().BeTrue();

        Factory.FakeEmail.Sent.Should().BeEmpty();
    }
}
