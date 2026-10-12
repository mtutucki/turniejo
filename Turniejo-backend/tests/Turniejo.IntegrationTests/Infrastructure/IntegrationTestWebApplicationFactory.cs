using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Turniejo.Api.BackgroundServices;
using Turniejo.Api.Extensions;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.IntegrationTests.Infrastructure;

// Jeden kontener Postgresa na cały przebieg testów (start ~2-5s, zbyt wolno na "raz na test") —
// izolację między testami zapewnia ResetDatabaseAsync (TRUNCATE wszystkich tabel), nie osobny
// kontener. 9 modułów = 9 DbContextów, każdy z własnym schematem w tej samej bazie.
public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("turniejo_integration")
        .WithUsername("turniejo")
        .WithPassword("turniejo")
        .Build();

    public FakeKeycloakAdminService FakeKeycloak { get; } = new();
    public FakeEmailSender FakeEmail { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString()
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IKeycloakAdminService>();
            services.AddSingleton<IKeycloakAdminService>(FakeKeycloak);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(FakeEmail);

            // OutboxProcessor/MatchReminderBackgroundService startują razem z hostem, czyli ZANIM
            // InitializeAsync zdąży wykonać migracje — uderzają w nieistniejące jeszcze tabele,
            // rzucają wyjątkiem, a domyślne BackgroundServiceExceptionBehavior.StopHost ubija
            // IServiceProvider hosta w trakcie naszych migracji (ObjectDisposedException).
            services.RemoveAll<IHostedService>();

            // Zarejestrowany jako zwykły serwis (nie IHostedService, więc nie wystartuje sam) —
            // testy wołają FlushOutboxAsync ręcznie, w konkretnym momencie po HTTP-callu, zamiast
            // czekać na 10-sekundowy interwał tła.
            services.AddSingleton<OutboxProcessor>();

            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                options.DefaultScheme = TestAuthHandler.SchemeName;
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Wymusza budowę hosta TERAZ (a nie leniwie przy pierwszym CreateClient) — connection
        // string z kontenera musi być gotowy zanim ConfigureWebHost go odczyta. CreateClient()
        // (a nie samo Services) jest tu celowe: dla top-level Program (minimal hosting) samo
        // odwołanie się do Services potrafi zwrócić provider z hosta, który zaraz potem zostaje
        // zutylizowany — CreateClient wymusza pełne, stabilne uruchomienie TestServera.
        using var _ = CreateClient();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.MigrateAllModulesAsync();
    }

    // Truncate zamiast osobnego kontenera per test — kilka rzędów wielkości szybsze, a przy jednym,
    // dzielonym kontenerze to jedyny sposób na to, żeby jeden test nie widział danych drugiego.
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        const string tablesSql =
            "SELECT table_schema, table_name FROM information_schema.tables " +
            "WHERE table_schema IN ('users','organizations','tournaments','teams','players','matches','standings','brackets','notifications') " +
            "AND table_type = 'BASE TABLE'";

        var tables = new List<string>();
        await using (var tablesCmd = new NpgsqlCommand(tablesSql, connection))
        await using (var tablesReader = await tablesCmd.ExecuteReaderAsync())
        {
            while (await tablesReader.ReadAsync())
                tables.Add($"\"{tablesReader.GetString(0)}\".\"{tablesReader.GetString(1)}\"");
        }

        if (tables.Count == 0)
            return;

        var truncateSql = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE;";
        await using var truncateCmd = new NpgsqlCommand(truncateSql, connection);
        await truncateCmd.ExecuteNonQueryAsync();

        FakeKeycloak.Reset();
        FakeEmail.Reset();
    }

    // Domenowe eventy (MatchFinished -> przeliczenie tabeli, awans w drabince, ScheduleGenerated
    // itd.) idą wyłącznie przez outbox — nie ma synchronicznego dispatchu przy SaveChanges. Bez
    // działającego OutboxProcessor w tle testy muszą same "przepompować" outbox w tym punkcie,
    // w którym produkcyjny kod czekałby na kolejny 10-sekundowy tick.
    public async Task FlushOutboxAsync(CancellationToken cancellationToken = default)
    {
        var processor = Services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessPendingMessagesAsync(cancellationToken);
    }

    // Jawna implementacja: WebApplicationFactory<T> ma już publiczne DisposeAsync() zwracające
    // ValueTask (IAsyncDisposable), a IAsyncLifetime.DisposeAsync() zwraca Task — nie da się tego
    // przeciążyć samym typem zwracanym, więc IAsyncLifetime musi być zaimplementowane jawnie.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
