namespace Turniejo.SharedKernel.Helpers;

public sealed class SlugGenerator
{
    public static string Generate(string name) =>
        name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("ą", "a").Replace("ć", "c")
            .Replace("ę", "e").Replace("ł", "l")
            .Replace("ń", "n").Replace("ó", "o")
            .Replace("ś", "s").Replace("ź", "z")
            .Replace("ż", "z")
            + "-" + Guid.NewGuid().ToString("N")[..6];
}
