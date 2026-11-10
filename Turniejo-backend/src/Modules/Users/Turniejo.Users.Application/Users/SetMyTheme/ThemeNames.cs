using Turniejo.Users.Domain.Enums;

namespace Turniejo.Users.Application.Users.SetMyTheme;

// W API motyw to tekst ("system", "light", "dark"), w bazie int z enuma.
public static class ThemeNames
{
    public static string ToName(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => "light",
        ThemePreference.Dark => "dark",
        _ => "system",
    };

    public static bool TryParse(string? name, out ThemePreference theme)
    {
        theme = name switch
        {
            "system" => ThemePreference.System,
            "light" => ThemePreference.Light,
            "dark" => ThemePreference.Dark,
            _ => (ThemePreference)(-1),
        };
        return Enum.IsDefined(theme);
    }
}
