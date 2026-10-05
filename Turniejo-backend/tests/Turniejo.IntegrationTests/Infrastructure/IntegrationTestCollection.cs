namespace Turniejo.IntegrationTests.Infrastructure;

// Jeden kontener Postgresa dzielony przez WSZYSTKIE klasy testowe w tej kolekcji — start kontenera
// (~2-5s) i 9x migracja to zbyt duży koszt na powtarzanie per klasa. Izolację zapewnia
// ResetDatabaseAsync wołane w konstruktorze/IAsyncLifetime każdej klasy testowej.
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestWebApplicationFactory>
{
    public const string Name = "Integration";
}
