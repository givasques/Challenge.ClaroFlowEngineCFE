using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Services;
using ClaroFlowEngine.Api.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaroFlowEngine.Api.Tests;

/// <summary>
/// Provedor openai_compatible contra servidor HTTP simulado (FASE 4.4, B.4): os nove cenários da spec e a verificação
/// de que a requisição que sai pela rede não contém dado pessoal.
/// </summary>
public class AiProviderTests
{
    private const string ApiKey = "chave-ficticia-de-teste";

    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);

    // Cenário 1
    [Fact]
    public async Task Cenario_1_resposta_valida_vira_resumo_da_ia()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        using var host = AiHost(server);
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await TestData.CreateCustomerAsync(host, TestCpfValue(), "Cliente Simulado Um");
        await TestData.AddJourneyAsync(host, customerId, Channels.Whatsapp, 30);

        var result = await GenerateAsync(host, customerId);

        Assert.Equal("ai", result.Source);
        Assert.Equal("Gerado por IA", result.SourceLabel);
        Assert.Equal("modelo-sim", result.Model);
        Assert.Equal("Cliente com 1 atendimento registrado, sobre troca de plano.", result.Summary);
        Assert.Single(result.AttentionPoints);
        Assert.Null(result.FallbackReason);
    }

    // Cenário 2
    [Fact]
    public async Task Cenario_2_texto_que_nao_e_json_cai_no_fallback()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = (context, _) => SimulatedAiServer.Respond(context, 200, SimulatedAiServer.Envelope("Claro, aqui está o resumo do cliente."));
        await AssertFallbackAsync(server, "ai_invalid_response");
    }

    // Cenário 3
    [Fact]
    public async Task Cenario_3_json_sem_um_dos_campos_cai_no_fallback()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = (context, _) => SimulatedAiServer.Respond(context, 200, SimulatedAiServer.Envelope(
            "{\"summary\":\"Resumo.\",\"suggested_approach\":\"Abordagem.\"}"));
        await AssertFallbackAsync(server, "ai_invalid_response");
    }

    // Cenário 4
    [Fact]
    public async Task Cenario_4_cpf_na_resposta_cai_no_fallback()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = (context, _) => SimulatedAiServer.Respond(context, 200, SimulatedAiServer.Envelope(
            "{\"summary\":\"Cliente do CPF 123.456.789-09 com troca de plano.\",\"attention_points\":[],\"suggested_approach\":\"Padrão.\"}"));
        await AssertFallbackAsync(server, "ai_invalid_response");
    }

    // Cenário 5
    [Fact]
    public async Task Cenario_5_demora_maior_que_o_timeout_responde_no_tempo_configurado()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = async (context, _) => await Task.Delay(TimeSpan.FromSeconds(30), context.RequestAborted);

        var watch = Stopwatch.StartNew();
        var result = await AssertFallbackAsync(server, "ai_timeout", timeoutSeconds: 1);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8), $"demorou {watch.Elapsed}");
        Assert.Equal("rules", result.Source);
    }

    // Cenário 6
    [Fact]
    public async Task Cenario_6_401_cai_no_fallback_sem_vazar_a_chave()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = (context, _) => SimulatedAiServer.Respond(context, 401, "{\"error\":\"invalid api key\"}");
        await AssertFallbackAsync(server, "ai_error");
    }

    // Cenário 7
    [Fact]
    public async Task Cenario_7_429_repete_uma_vez_e_cai_no_fallback_de_limite()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = (context, _) => SimulatedAiServer.Respond(context, 429, "{\"error\":\"rate limit\"}");

        await AssertFallbackAsync(server, "ai_rate_limited");
        Assert.Equal(2, server.Requests.Count); // tentativa, retry curto, fallback
    }

    // Cenário 8
    [Fact]
    public async Task Cenario_8_provedor_que_recusa_response_format_recebe_nova_tentativa_sem_o_campo()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        server.Handler = (context, body) =>
        {
            if (body.Contains("response_format"))
                return SimulatedAiServer.Respond(context, 400, "{\"error\":\"Unrecognized request argument supplied: response_format\"}");
            return SimulatedAiServer.Respond(context, 200, SimulatedAiServer.Envelope(SimulatedAiServer.ValidContent));
        };
        using var host = AiHost(server);
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await TestData.CreateCustomerAsync(host, TestCpfValue(), "Cliente Simulado Oito");
        await TestData.AddJourneyAsync(host, customerId, Channels.Whatsapp, 30);

        var result = await GenerateAsync(host, customerId);

        Assert.Equal("ai", result.Source);
        Assert.Equal(2, server.Requests.Count);
        var requests = server.Requests.ToArray();
        Assert.Contains("response_format", requests[0].Body);
        Assert.DoesNotContain("response_format", requests[1].Body);
    }

    // Cenário 9
    [Fact]
    public async Task Cenario_9_servidor_fora_do_ar_cai_no_fallback()
    {
        int closedPort;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        using var host = new TestHost(options =>
        {
            options.Provider = OpenAiCompatibleSummaryProvider.ProviderName;
            options.BaseUrl = $"http://127.0.0.1:{closedPort}/v1";
            options.Model = "modelo-sim";
            options.ApiKey = ApiKey;
            options.TimeoutSeconds = 5;
        });
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await TestData.CreateCustomerAsync(host, TestCpfValue(), "Cliente Simulado Nove");
        await TestData.AddJourneyAsync(host, customerId, Channels.Whatsapp, 30);

        var result = await GenerateAsync(host, customerId);

        Assert.Equal("rules", result.Source);
        Assert.Equal("ai_error", result.FallbackReason);
    }

    // B.4: o que sai pela rede não contém dado pessoal, e a chave vai só no cabeçalho de autorização.
    [Fact]
    public async Task Requisicao_que_sai_pela_rede_nao_contem_dado_pessoal_nem_chave_no_corpo()
    {
        await using var server = await SimulatedAiServer.StartAsync();
        using var host = AiHost(server);
        await TestHost.EnsureDatabaseAsync(host.Services);

        var cpf = TestCpfValue();
        var customerId = await TestData.CreateCustomerAsync(host, cpf, "Maria Teste Pereira");
        await TestData.AddJourneyAsync(host, customerId, Channels.Whatsapp, 30);
        var phone = "5511" + Random.Shared.NextInt64(100000000, 999999999);
        var appAccount = "maria.teste." + Guid.NewGuid().ToString("N")[..8];
        await AddIdentityLinksAsync(host, customerId, phone, appAccount);

        await GenerateAsync(host, customerId);

        var request = server.Requests.Single();
        var body = request.Body;
        Assert.DoesNotContain("Maria", body);
        Assert.DoesNotContain("Pereira", body);
        Assert.DoesNotContain(cpf, body);
        Assert.DoesNotContain(phone, body);
        Assert.DoesNotContain(appAccount, body, StringComparison.OrdinalIgnoreCase);
        Assert.False(GuidPattern.IsMatch(body), "a requisição contém um GUID");
        Assert.DoesNotContain(ApiKey, body);
        Assert.Equal($"Bearer {ApiKey}", request.Authorization);
    }

    private static TestHost AiHost(SimulatedAiServer server) =>
        new(options =>
        {
            options.Provider = OpenAiCompatibleSummaryProvider.ProviderName;
            options.BaseUrl = server.BaseUrl;
            options.Model = "modelo-sim";
            options.ApiKey = ApiKey;
            options.TimeoutSeconds = 5;
        });

    private static async Task<CustomerSummaryResponseView> GenerateAsync(TestHost host, Guid customerId)
    {
        using var scope = host.Services.CreateScope();
        var response = await scope.ServiceProvider.GetRequiredService<ICustomerSummaryService>()
            .GenerateAsync(customerId, forceRefresh: true, CancellationToken.None);
        return new CustomerSummaryResponseView(response.Summary, response.AttentionPoints, response.Source, response.SourceLabel, response.Model, response.FallbackReason);
    }

    private static async Task<CustomerSummaryResponseView> AssertFallbackAsync(SimulatedAiServer server, string expectedReason, int timeoutSeconds = 5)
    {
        using var host = new TestHost(options =>
        {
            options.Provider = OpenAiCompatibleSummaryProvider.ProviderName;
            options.BaseUrl = server.BaseUrl;
            options.Model = "modelo-sim";
            options.ApiKey = ApiKey;
            options.TimeoutSeconds = timeoutSeconds;
        });
        await TestHost.EnsureDatabaseAsync(host.Services);
        var customerId = await TestData.CreateCustomerAsync(host, TestCpfValue(), "Cliente Simulado Fallback");
        await TestData.AddJourneyAsync(host, customerId, Channels.Whatsapp, 30);

        var result = await GenerateAsync(host, customerId);

        Assert.Equal("rules", result.Source);
        Assert.Equal(expectedReason, result.FallbackReason);
        Assert.Equal("IA indisponível no momento", result.FallbackLabel);
        Assert.Equal(0, await TestData.CountAiSummariesAsync(host, customerId));
        return result;
    }

    private static async Task AddIdentityLinksAsync(TestHost host, Guid customerId, string phone, string appAccount)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        db.IdentityLinks.Add(new IdentityLink { CustomerId = customerId, Channel = Channels.Whatsapp, Identifier = phone });
        db.IdentityLinks.Add(new IdentityLink { CustomerId = customerId, Channel = Channels.App, Identifier = appAccount });
        await db.SaveChangesAsync();
    }

    /// <summary>CPF de teste aleatório e válido, para não colidir entre execuções.</summary>
    private static string TestCpfValue() => TestCpf.New();

    private sealed record CustomerSummaryResponseView(
        string Summary, IReadOnlyList<string> AttentionPoints, string Source, string SourceLabel, string? Model, string? FallbackReason)
    {
        public string? FallbackLabel => FallbackReason is null ? null : "IA indisponível no momento";
    }
}
