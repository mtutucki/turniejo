using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Turniejo.Notifications.Application.Notifications.CreateMatchReminderNotifications;

namespace Turniejo.Architecture.UnitTests
{
    // CreateMatchReminderNotificationsCommand jest IPublicRequest (pomija KAŻDY check
    // AuthorizationBehavior) — bezpieczne dziś tylko dlatego, że żaden kontroler go nie przyjmuje;
    // wywołuje je wyłącznie MatchReminderBackgroundService przez ISender. Gdyby ktoś kiedyś dopisał
    // do kontrolera `[FromBody] CreateMatchReminderNotificationsCommand`, stałoby się to
    // nieuwierzytelnionym wyzwalaczem masowej wysyłki powiadomień, dostępnym dla każdego.
    public sealed class PublicRequestSafetyTests
    {
        [Fact]
        public void CreateMatchReminderNotificationsCommand_ShouldNeverBeAcceptedByAControllerAction()
        {
            var commandType = typeof(CreateMatchReminderNotificationsCommand);

            var offendingActions = typeof(Turniejo.Api.Controllers.PublicController).Assembly
                .GetTypes()
                .Where(t => typeof(ControllerBase).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.GetParameters().Any(p => p.ParameterType == commandType))
                .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
                .ToList();

            offendingActions.Should().BeEmpty(
                "CreateMatchReminderNotificationsCommand is IPublicRequest (no auth check at all) and must " +
                "stay reachable only from MatchReminderBackgroundService, never from an HTTP controller action");
        }
    }
}
