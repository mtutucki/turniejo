namespace Turniejo.IntegrationTests.Infrastructure;

[Collection(IntegrationTestCollection.Name)]
public abstract class IntegrationTestBase(IntegrationTestWebApplicationFactory factory) : IAsyncLifetime
{
    protected IntegrationTestWebApplicationFactory Factory { get; } = factory;

    // Jedna wspólna baza, więc każdy test musi zaczynać na czysto — bez tego test B widziałby
    // dane zostawione przez test A (np. drugi user o tym samym e-mailu -> fałszywy 409).
    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    protected HttpClient CreateClient() => Factory.CreateClient();

    protected HttpClient CreateAuthenticatedClient(Guid userId, string? email = null)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email ?? $"{userId}@integration.test");
        return client;
    }

    protected Task FlushOutboxAsync() => Factory.FlushOutboxAsync();
}
