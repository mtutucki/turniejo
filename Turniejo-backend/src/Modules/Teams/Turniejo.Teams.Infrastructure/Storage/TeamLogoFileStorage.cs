using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Turniejo.Teams.Application.Abstractions;

namespace Turniejo.Teams.Infrastructure.Storage;

/// <summary>
/// Herby na dysku serwera: {Uploads:Path}/team-logos/{guid}.{ext}. API serwuje katalog Uploads:Path
/// pod /uploads (Program.cs). W produkcji to wolumen Dockera, który trafia też do backupu.
/// </summary>
public sealed partial class TeamLogoFileStorage(string uploadsRoot) : ITeamLogoStorage
{
    private const string Folder = "team-logos";

    private string Directory => Path.Combine(uploadsRoot, Folder);

    /// <summary>Katalog na wgrane pliki. Domyślnie "uploads" obok aplikacji (lokalnie w katalogu projektu API).</summary>
    public static string ResolveUploadsRoot(IConfiguration configuration) =>
        Path.GetFullPath(configuration["Uploads:Path"] is { Length: > 0 } path ? path : "uploads");

    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var fileName = $"{Guid.NewGuid():N}.{extension}";
        await File.WriteAllBytesAsync(Path.Combine(Directory, fileName), content, cancellationToken);
        return ITeamLogoStorage.PublicPrefix + fileName;
    }

    public void DeleteIfStored(string? logoUrl)
    {
        if (logoUrl == null || !logoUrl.StartsWith(ITeamLogoStorage.PublicPrefix, StringComparison.Ordinal))
            return;

        // Tylko nazwy, które sami nadaliśmy. Nic w rodzaju "../" nie przejdzie.
        var fileName = logoUrl[ITeamLogoStorage.PublicPrefix.Length..];
        if (!StoredFileName().IsMatch(fileName))
            return;

        var path = Path.Combine(Directory, fileName);
        if (File.Exists(path))
            File.Delete(path);
    }

    [GeneratedRegex("^[0-9a-f]{32}\\.(png|jpg|webp)$")]
    private static partial Regex StoredFileName();
}
