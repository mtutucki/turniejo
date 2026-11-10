using Turniejo.SharedKernel.Contracts;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Teams.Application.TournamentTeams
{
    /// <summary>
    /// Mecze powstają z listy drużyn i podziału na grupy w chwili generowania terminarza. Późniejsza zmiana
    /// grupy albo nowa drużyna nie zmienia meczów, więc terminarz i tabele by się rozjechały.
    /// Organizator najpierw czyści terminarz (turniej wraca do „Zapisy zamknięte”).
    /// </summary>
    internal static class ScheduleLock
    {
        private static readonly HashSet<string> LockedStatuses = ["ScheduleGenerated", "InProgress", "Finished"];

        public static readonly Error Error =
            Error.Conflict("Schedule", "Clear the schedule before changing teams or groups");

        public static bool IsLocked(TournamentDto tournament) => LockedStatuses.Contains(tournament.Status);
    }
}
