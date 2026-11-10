using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Turniejo.SharedKernel.Helpers;

public sealed partial class SlugGenerator
{
    /// <summary>
    /// Slug do adresu: same małe litery bez ogonków, cyfry i pojedyncze myślniki, plus losowy sufiks.
    /// Znaki takie jak „:” czy „/” w adresie trzeba kodować, a to psuło portale („8:00 - 10:00”).
    /// </summary>
    public static string Generate(string name)
    {
        var slug = NonSlugChars().Replace(RemoveDiacritics(name.ToLowerInvariant()), "-").Trim('-');
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return slug.Length == 0 ? suffix : $"{slug}-{suffix}";
    }

    // „ł” nie rozkłada się na literę i znak diakrytyczny, więc zamiana ręczna.
    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                result.Append(c);
        return result.ToString();
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();
}
