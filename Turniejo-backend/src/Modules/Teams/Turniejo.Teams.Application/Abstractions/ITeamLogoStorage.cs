namespace Turniejo.Teams.Application.Abstractions
{
    /// <summary>Herby drużyn wgrane jako pliki. Adres publiczny to <see cref="PublicPrefix"/> + nazwa pliku.</summary>
    public interface ITeamLogoStorage
    {
        const string PublicPrefix = "/uploads/team-logos/";

        /// <summary>Zapisuje plik pod losową nazwą i zwraca jego publiczny adres (względny).</summary>
        Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default);

        /// <summary>Usuwa plik, jeśli adres wskazuje na wgrany herb. Adresy zewnętrzne ignoruje.</summary>
        void DeleteIfStored(string? logoUrl);
    }
}
