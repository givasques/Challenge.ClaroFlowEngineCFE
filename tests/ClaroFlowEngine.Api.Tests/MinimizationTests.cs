using System.Text.Json;
using System.Text.RegularExpressions;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Services;
using ClaroFlowEngine.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaroFlowEngine.Api.Tests;

/// <summary>
/// Teste obrigatório de minimização (FASE 4.4, A.3): o que sai para o modelo não contém dado que identifique a pessoa.
/// Roda sobre os clientes de demonstração do seed, e sobre o texto do resumo por regras gerado a partir do retrato.
/// </summary>
public class MinimizationTests
{
    private static readonly string[] DemoCpfs =
    [
        "11144477735", "22255588846", "33366699957",
        "52601815906", "08301661305", "18609139034", "99603082430", "62819482112", "49100528102",
    ];

    private static readonly Regex GuidPattern = new(
        @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}",
        RegexOptions.Compiled);

    private static readonly string[] ForbiddenKeys =
    [
        "FullName", "full_name", "Cpf", "cpf", "Phone", "phone", "Identifier", "identifier",
        "CustomerId", "customer_id", "PanelUser", "panel_user", "Email", "email",
    ];

    [Fact]
    public async Task Retrato_dos_clientes_de_demonstracao_nao_contem_dado_pessoal()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var collector = scope.ServiceProvider.GetRequiredService<ICustomerHistoryCollector>();

        var customers = await db.Customers.AsNoTracking().Where(c => DemoCpfs.Contains(c.Cpf)).ToListAsync();
        Assert.Equal(DemoCpfs.Length, customers.Count);

        var identities = await db.IdentityLinks.AsNoTracking()
            .Where(l => customers.Select(c => c.Id).Contains(l.CustomerId))
            .ToListAsync();

        foreach (var customer in customers)
        {
            var history = await collector.CollectAsync(customer.Id, CancellationToken.None);
            var json = JsonSerializer.Serialize(history.Portrait);

            AssertNoPersonalData(json, customer, identities.Where(l => l.CustomerId == customer.Id).ToList());

            var rules = await new RuleBasedSummaryProvider().GenerateAsync(history.Portrait, CancellationToken.None);
            var text = string.Join(' ', [rules.Content.Summary, .. rules.Content.AttentionPoints, rules.Content.SuggestedApproach ?? string.Empty]);
            AssertNoPersonalData(text, customer, identities.Where(l => l.CustomerId == customer.Id).ToList());
        }
    }

    [Fact]
    public async Task Retrato_traz_jornadas_e_planos_quando_o_cliente_os_tem()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var collector = scope.ServiceProvider.GetRequiredService<ICustomerHistoryCollector>();

        // Lucia tem uma jornada aberta (do cenário da Central) e nenhum plano; Ana tem plano contratado.
        var lucia = await db.Customers.AsNoTracking().FirstAsync(c => c.Cpf == "52601815906");
        var ana = await db.Customers.AsNoTracking().FirstAsync(c => c.Cpf == "11144477735");

        var luciaHistory = await collector.CollectAsync(lucia.Id, CancellationToken.None);
        Assert.NotEmpty(luciaHistory.Portrait.Journeys);
        Assert.Empty(luciaHistory.Portrait.Plans);
        Assert.Equal(64, luciaHistory.Fingerprint.Length);

        var anaHistory = await collector.CollectAsync(ana.Id, CancellationToken.None);
        Assert.NotEmpty(anaHistory.Portrait.Plans);
    }

    /// <summary>
    /// Só grava exemplos do retrato quando CFE_TEST_DUMP_DIR está definida (para colar no relatório). Não faz nenhuma asserção.
    /// </summary>
    [Fact]
    public async Task Grava_exemplos_do_retrato_quando_solicitado()
    {
        var dumpDir = Environment.GetEnvironmentVariable("CFE_TEST_DUMP_DIR");
        if (string.IsNullOrWhiteSpace(dumpDir)) return;

        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var collector = scope.ServiceProvider.GetRequiredService<ICustomerHistoryCollector>();
        Directory.CreateDirectory(dumpDir);

        foreach (var cpf in DemoCpfs)
        {
            var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Cpf == cpf);
            var history = await collector.CollectAsync(customer.Id, CancellationToken.None);
            var json = JsonSerializer.Serialize(history.Portrait, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(dumpDir, $"retrato-{cpf}.json"), json);
        }
    }

    private static void AssertNoPersonalData(string text, Customer customer, IReadOnlyList<IdentityLink> identities)
    {
        // Nome completo e cada parte com 3 ou mais letras (ex.: "Lucia", "Ferreira").
        foreach (var part in customer.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(p => p.Length >= 3))
            Assert.DoesNotContain(part, text, StringComparison.OrdinalIgnoreCase);

        // CPF completo e qualquer dígito-sequência de 11 do cliente.
        Assert.DoesNotContain(customer.Cpf, text);

        // Identificadores de canal: telefone (dígitos) e conta do App.
        foreach (var link in identities)
        {
            if (link.Channel == "cpf") continue;
            Assert.DoesNotContain(link.Identifier, text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.False(GuidPattern.IsMatch(text), "texto contém um GUID");

        foreach (var key in ForbiddenKeys)
            Assert.DoesNotContain($"\"{key}\"", text);
    }
}
