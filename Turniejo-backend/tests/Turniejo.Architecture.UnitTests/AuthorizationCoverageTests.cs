using System.Reflection;
using FluentAssertions;
using Turniejo.SharedKernel.CQRS;

namespace Turniejo.Architecture.UnitTests
{
    // Wymusza fail-closed by default: AuthorizationBehavior sprawdza rolę/scope tylko gdy
    // Command/Query jawnie implementuje IAuthorizedRequest, a pomija WSZYSTKO (nawet
    // IsAuthenticated) dla IPublicRequest. Bez tego testu nowy Command/Query bez żadnego markera
    // przechodzi przez pipeline z samym sprawdzeniem "czy zalogowany" — cicha, łatwa do przeoczenia
    // luka (dokładnie to znalazł audyt dla AssignUserRoleCommand/RemoveUserRoleCommand/GetUserRolesQuery).
    public sealed class AuthorizationCoverageTests
    {
        private static readonly Assembly[] ApplicationAssemblies =
        [
            typeof(Turniejo.Users.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Tournaments.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Teams.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Matches.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Notifications.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Organizations.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Players.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Standings.Application.AssemblyReference).Assembly,
            typeof(Turniejo.Brackets.Application.AssemblyReference).Assembly,
        ];

        [Fact]
        public void EveryCommandAndQuery_ShouldDeclareAuthorizedOrPublic()
        {
            var offenders = ApplicationAssemblies
                .SelectMany(a => a.GetTypes())
                .Where(t => t is { IsClass: true, IsAbstract: false })
                .Where(IsMediatrRequest)
                .Where(t =>
                    !typeof(IAuthorizedRequest).IsAssignableFrom(t) &&
                    !typeof(IPublicRequest).IsAssignableFrom(t) &&
                    !typeof(IAuthenticatedOnlyRequest).IsAssignableFrom(t))
                .Select(t => t.FullName)
                .OrderBy(name => name)
                .ToList();

            offenders.Should().BeEmpty(
                "every Command/Query must explicitly implement IAuthorizedRequest (checked role/scope), " +
                "IPublicRequest (deliberately no auth check) or IAuthenticatedOnlyRequest (deliberately just " +
                "logged-in, e.g. self-filtered by CurrentUserId) — an unmarked request only gets the bare " +
                "IsAuthenticated check from AuthorizationBehavior by accident, not by decision. " +
                $"Offenders: {string.Join(", ", offenders)}");
        }

        private static bool IsMediatrRequest(Type type) =>
            type.GetInterfaces().Any(i =>
                i == typeof(ICommand) ||
                (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommand<>)) ||
                (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)));
    }
}
