namespace Turniejo.SharedKernel.Helpers;

/// <summary>
/// Nazwisko zawodnika na portalu publicznym. Przy TournamentPublicSettings.AbbreviatePlayerNames
/// (domyślnie włączone — zawodnicy to zwykle dzieci) zostaje tylko inicjał: "Kowalski" → "K.".
/// </summary>
public static class PublicPlayerName
{
    public static string LastName(string lastName, bool abbreviate)
    {
        if (!abbreviate)
            return lastName;

        var trimmed = lastName.Trim();
        return trimmed.Length == 0 ? trimmed : $"{char.ToUpperInvariant(trimmed[0])}.";
    }
}
