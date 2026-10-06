using System.Diagnostics;
using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Services;
using ClaroFlowEngine.Api.Modules.Lgpd.Services;
using ClaroFlowEngine.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaroFlowEngine.Api.Tests;

/// <summary>Resumo do cliente com IA (FASE 4.4, A.5 a A.8), contra o banco real.</summary>
public class CustomerSummaryServiceTests
{
    // CPFs novos a cada execução (ver TestCpf): o banco de teste persiste entre execuções.
    private static readonly string CpfNoJourney = TestCpf.New();
    private static readonly string CpfCache = TestCpf.New();
    private static readonly string CpfFallback = TestCpf.New();
    private static readonly string CpfTimeout = TestCpf.New();
    private static readonly string CpfRateLimit = TestCpf.New();
    private static readonly string CpfAnonymized = TestCpf.New();

    [Fact]
    public async Task Cliente_sem_jornada_recebe_o_texto_fixo_sem_pontos_nem_sugestao()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await CreateCustomerAsync(host, CpfNoJourney, "Cliente Sem Historico");

        using var scope = host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>()
            .GenerateAsync(customerId, forceRefresh: false, CancellationToken.None);

        Assert.Equal("Ainda não há atendimentos registrados para este cliente.", result.Summary);
        Assert.Empty(result.AttentionPoints);
        Assert.Null(result.SuggestedApproach);
        Assert.Equal("rules", result.Source);
        Assert.Equal(0, host.Provider.Calls);
    }

    [Fact]
    public async Task Resumo_da_ia_fica_em_cache_enquanto_os_dados_nao_mudam()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await CreateCustomerAsync(host, CpfCache, "Cliente Cache");
        await AddJourneyAsync(host, customerId, Channels.Whatsapp, minutesAgo: 30);

        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>();

        var first = await service.GenerateAsync(customerId, false, CancellationToken.None);
        Assert.Equal("ai", first.Source);
        Assert.False(first.Cached);
        Assert.Equal(1, host.Provider.Calls);

        var second = await service.GenerateAsync(customerId, false, CancellationToken.None);
        Assert.True(second.Cached);
        Assert.Equal(1, host.Provider.Calls);
        Assert.Equal("Gerado por IA", second.SourceLabel);

        // Nova jornada muda a impressão digital: gera de novo, e continua guardada só uma linha de IA.
        await AddJourneyAsync(host, customerId, Channels.App, minutesAgo: 5);
        var third = await service.GenerateAsync(customerId, false, CancellationToken.None);
        Assert.False(third.Cached);
        Assert.Equal(2, host.Provider.Calls);
        Assert.Equal(1, await CountAiSummariesAsync(host, customerId));
    }

    [Fact]
    public async Task Falha_da_ia_volta_o_resumo_por_regras_com_o_motivo_e_nao_guarda_nada()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await CreateCustomerAsync(host, CpfFallback, "Cliente Fallback");
        await AddJourneyAsync(host, customerId, Channels.Whatsapp, minutesAgo: 30);
        host.Provider.Behavior = (_, _) => throw new AiProviderException("ai_invalid_response", "resposta sem campos");

        using var scope = host.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>()
            .GenerateAsync(customerId, false, CancellationToken.None);

        Assert.Equal("rules", result.Source);
        Assert.Equal("Resumo automático por regras", result.SourceLabel);
        Assert.Equal("ai_invalid_response", result.FallbackReason);
        Assert.Equal("IA indisponível no momento", result.FallbackLabel);
        Assert.Equal(0, await CountAiSummariesAsync(host, customerId));
    }

    [Fact]
    public async Task Ia_que_demora_mais_que_o_timeout_cai_no_fallback_no_tempo_configurado()
    {
        using var host = new TestHost(options => options.TimeoutSeconds = 1);
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await CreateCustomerAsync(host, CpfTimeout, "Cliente Timeout");
        await AddJourneyAsync(host, customerId, Channels.Whatsapp, minutesAgo: 30);
        host.Provider.Behavior = async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            throw new InvalidOperationException("não deveria chegar aqui");
        };

        using var scope = host.Services.CreateScope();
        var watch = Stopwatch.StartNew();
        var result = await scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>()
            .GenerateAsync(customerId, false, CancellationToken.None);
        watch.Stop();

        Assert.Equal("ai_timeout", result.FallbackReason);
        Assert.Equal("rules", result.Source);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"demorou {watch.Elapsed}");
    }

    [Fact]
    public async Task Limite_por_atendente_conta_geracoes_e_nao_respostas_do_cache()
    {
        using var host = new TestHost(options => options.RequestsPerUserPerMinute = 1);
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await CreateCustomerAsync(host, CpfRateLimit, "Cliente Limite");
        await AddJourneyAsync(host, customerId, Channels.Whatsapp, minutesAgo: 30);

        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>();

        // 1ª: geração (consome a única permissão). 2ª: cache, não consome. 3ª forçada: 429.
        var first = await service.GenerateAsync(customerId, false, CancellationToken.None);
        Assert.False(first.Cached);
        var cached = await service.GenerateAsync(customerId, false, CancellationToken.None);
        Assert.True(cached.Cached);

        var error = await Assert.ThrowsAsync<TooManyRequestsException>(
            () => service.GenerateAsync(customerId, forceRefresh: true, CancellationToken.None));
        Assert.Equal("too_many_requests", error.ErrorCode);
    }

    [Fact]
    public async Task Cliente_inexistente_retorna_nao_encontrado()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>()
                .GenerateAsync(Guid.NewGuid(), false, CancellationToken.None));
        Assert.Equal("customer_not_found", error.ErrorCode);
    }

    [Fact]
    public async Task Anonimizar_o_cliente_apaga_os_resumos_e_bloqueia_novos()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await CreateCustomerAsync(host, CpfAnonymized, "Cliente Anonimizar");
        await AddJourneyAsync(host, customerId, Channels.Whatsapp, minutesAgo: 30);

        using var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>();
        await service.GenerateAsync(customerId, false, CancellationToken.None);
        Assert.Equal(1, await CountAiSummariesAsync(host, customerId));

        await scope.ServiceProvider.GetRequiredService<ILgpdService>()
            .ExerciseRightToBeForgottenAsync(CpfAnonymized, CancellationToken.None);

        Assert.Equal(0, await CountAiSummariesAsync(host, customerId));
        var error = await Assert.ThrowsAsync<ConflictException>(
            () => service.GenerateAsync(customerId, forceRefresh: true, CancellationToken.None));
        Assert.Equal("already_anonymized", error.ErrorCode);
    }

    private static async Task<Guid> CreateCustomerAsync(TestHost host, string cpf, string fullName)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var customer = new Customer { Id = Guid.NewGuid(), Cpf = cpf, FullName = fullName, Segment = "Teste", BillingDueDay = 10 };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    private static async Task AddJourneyAsync(TestHost host, Guid customerId, string channel, int minutesAgo)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var created = DateTime.UtcNow.AddMinutes(-minutesAgo - 10);
        db.JourneyContexts.Add(new JourneyContext
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OriginChannel = channel,
            Intent = "change_plan",
            CurrentStep = "plan_selected",
            Status = JourneyStatus.Open,
            CreatedAt = created,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
        });
        await db.SaveChangesAsync();
    }

    private static async Task<int> CountAiSummariesAsync(TestHost host, Guid customerId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        return await db.CustomerAiSummaries.CountAsync(s => s.CustomerId == customerId);
    }
}
