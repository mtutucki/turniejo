namespace Turniejo.Users.Application.Abstractions;

public sealed class AppOptions
{
    /// <summary>Publiczny adres frontendu — baza linków w mailach (np. https://turniejo.pl).</summary>
    public string PublicUrl { get; set; } = string.Empty;
}
