using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Seed;
using ClaroFlowEngine.Api.Modules.CustomerInsights;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using ClaroFlowEngine.Api.Modules.Lgpd.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Tests.Support;

/// <summary>
/// Monta a DI com os serviços reais do módulo (banco de verdade, banco de teste separado) e um provedor controlado.
/// A conexão vem de CFE_TEST_CONNECTION; o padrão aponta para o banco cfe_tests do modo full local.
/// </summary>
public sealed class TestHost : IDisposable
{
    private static readonly SemaphoreSlim MigrationGate = new(1, 1);
    private static bool _migrated;

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("CFE_TEST_CONNECTION")
        ?? "Host=localhost;Port=5434;Database=cfe_tests;Username=cfe;Password=cfe_local_pwd";

    public ServiceProvider Services { get; }
    public ScriptedProvider Provider { get; }

    public TestHost(Action<AiSummaryOptions>? configure = null)
    {
        Provider = new ScriptedProvider();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CfeDbContext>(options => options
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention());
        services.Configure<CfeOptions>(_ => { });
        services.AddScoped<ITransitionRecorder, TransitionRecorder>();
        services.AddScoped<ICurrentPanelUserAccessor, FixedPanelUser>();
        services.AddScoped<ICurrentChannelAccessor, FixedChannel>();
        services.AddScoped<IJourneyExpirationService, JourneyExpirationService>();
        services.AddScoped<ILgpdService, LgpdService>();

        services.Configure<AiSummaryOptions>(options =>
        {
            options.Provider = ScriptedProvider.ProviderName;
            options.BaseUrl = "http://localhost:9";
            options.Model = "modelo-de-teste";
            options.ApiKey = "chave-ficticia-de-teste";
            options.TimeoutSeconds = 5;
            options.RequestsPerUserPerMinute = 100;
            configure?.Invoke(options);
        });

        services.AddCustomerInsightsModule();
        services.AddSingleton<ICustomerSummaryProvider>(Provider);

        Services = services.BuildServiceProvider();
    }

    /// <summary>Aplica as migrations e o seed uma vez por processo (o seed é idempotente).</summary>
    public static async Task EnsureDatabaseAsync(IServiceProvider services)
    {
        await MigrationGate.WaitAsync();
        try
        {
            if (_migrated) return;
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
            await db.Database.MigrateAsync();
            await DatabaseSeeder.SeedAsync(db);
            _migrated = true;
        }
        finally
        {
            MigrationGate.Release();
        }
    }

    public void Dispose() => Services.Dispose();
}

internal sealed class FixedPanelUser : ICurrentPanelUserAccessor
{
    public Guid? UserId => Guid.Parse("11111111-1111-1111-1111-111111111111");
    public string? FullName => "Gestor de teste";
    public string? Role => "manager";
}

internal sealed class FixedChannel : ICurrentChannelAccessor
{
    public string? Channel { get; set; } = Channels.Panel;
}

/// <summary>Provedor de teste: o comportamento de cada chamada é definido pelo teste, e o número de chamadas é contado.</summary>
public sealed class ScriptedProvider : ICustomerSummaryProvider
{
    public const string ProviderName = "scripted";

    public int Calls { get; private set; }

    public Func<CustomerPortrait, CancellationToken, Task<CustomerSummaryResult>> Behavior { get; set; } = DefaultBehavior;

    public string Name => ProviderName;

    public Task<CustomerSummaryResult> GenerateAsync(CustomerPortrait portrait, CancellationToken cancellationToken)
    {
        Calls++;
        return Behavior(portrait, cancellationToken);
    }

    public static Task<CustomerSummaryResult> DefaultBehavior(CustomerPortrait portrait, CancellationToken cancellationToken) =>
        Task.FromResult(new CustomerSummaryResult(
            new CustomerSummaryContent(
                "Resumo gerado pelo teste.",
                ["Ponto de teste."],
                "Abordagem de teste."),
            "ai",
            "modelo-de-teste"));
}
