namespace Turniejo.Tournaments.Domain.Enums
{
    public enum TournamentFormat
    {
        Group = 1,
        Knockout = 2,
        GroupAndKnockout = 3,
        // Faza 1: grupy jak w Group; faza 2: grupy obsadzane miejscami z tabel fazy 1.
        TwoGroupStages = 4,
    }
}