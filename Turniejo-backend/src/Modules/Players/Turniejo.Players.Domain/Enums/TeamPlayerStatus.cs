namespace Turniejo.Players.Domain.Enums
{
    public enum TeamPlayerStatus
    {
        Active = 1,
        // Odszedł z drużyny: profil i historia zostają, zawodnik nie jest już proponowany do składów.
        Left = 2
    }
}
