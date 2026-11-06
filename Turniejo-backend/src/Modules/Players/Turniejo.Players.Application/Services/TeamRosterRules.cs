using Turniejo.Players.Domain.Entities;
using Turniejo.Players.Domain.Enums;

namespace Turniejo.Players.Application.Services
{
    public static class TeamRosterRules
    {
        // Ten sam zawodnik dodany drugi raz: to samo imię, nazwisko i data urodzenia.
        public static bool HasNameTwin(IEnumerable<Player> profiles, string firstName, string lastName, DateTime birthDate, Guid? exceptPlayerId = null) =>
            profiles.Any(p =>
                p.Id != exceptPlayerId &&
                string.Equals(p.FirstName.Trim(), firstName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.LastName.Trim(), lastName.Trim(), StringComparison.OrdinalIgnoreCase) &&
                p.BirthDate.Date == birthDate.Date);

        // Numer musi być wolny wśród zawodników, którzy są w kadrze. Ci, którzy odeszli, go nie blokują.
        public static bool IsShirtNumberTaken(IEnumerable<TeamPlayer> members, int? shirtNumber, Guid? exceptPlayerId) =>
            shirtNumber.HasValue && members.Any(m =>
                m.Status == TeamPlayerStatus.Active &&
                m.PlayerId != exceptPlayerId &&
                m.ShirtNumber == shirtNumber);
    }
}
