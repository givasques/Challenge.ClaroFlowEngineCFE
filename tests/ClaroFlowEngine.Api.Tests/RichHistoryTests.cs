using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Services;
using ClaroFlowEngine.Api.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClaroFlowEngine.Api.Tests;

/// <summary>
/// Cliente de demonstração com histórico variado (FASE 4.4, D.1): o retrato e o resumo por regras precisam refletir
/// o que aconteceu com ele (plano abandonado, contestação escalada, contestação concluída, oportunidade crítica).
/// </summary>
public class RichHistoryTests
{
    private const string RodrigoCpf = "49100528102";

    [Fact]
    public async Task Cliente_com_historico_rico_tem_quatro_jornadas_e_o_resumo_aponta_o_que_importa()
    {
        using var host = new TestHost();
        await TestHost.EnsureDatabaseAsync(host.Services);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var customer = await db.Customers.AsNoTracking().FirstAsync(c => c.Cpf == RodrigoCpf);

        var history = await scope.ServiceProvider.GetRequiredService<ICustomerHistoryCollector>()
            .CollectAsync(customer.Id, CancellationToken.None);
        var portrait = history.Portrait;

        Assert.Equal(4, portrait.Counts.Total);
        Assert.Equal(1, portrait.Counts.Escalated);
        Assert.Equal(1, portrait.Counts.Abandoned);
        Assert.Equal(2, portrait.Counts.Concluded); // contestação concluída e troca de plano concluída
        Assert.Equal(2, portrait.Disputes.Count);
        Assert.Single(portrait.Opportunities);

        var rules = await new RuleBasedSummaryProvider().GenerateAsync(portrait, CancellationToken.None);
        var points = string.Join(" | ", rules.Content.AttentionPoints);

        Assert.StartsWith("Cliente com 4 atendimentos registrados, principalmente sobre contestação de cobrança.", rules.Content.Summary);
        // A.5: no máximo 4 pontos, por ordem de prioridade (escalada, contestação, abandono, oportunidade, parada).
        // Este cliente tem cinco sinais; a oportunidade crítica fica de fora porque os quatro primeiros ocupam o limite.
        Assert.Equal(4, rules.Content.AttentionPoints.Count);
        Assert.True(points.Contains("escalada para Financeiro"), points);
        Assert.True(points.Contains("Contestou a fatura"), points);
        Assert.True(points.Contains("Troca de plano abandonada"), points);
        Assert.DoesNotContain("Oportunidade crítica aberta", points);
        // A escalada sem desfecho tem prioridade na sugestão.
        Assert.Equal("Confirmar o andamento da escalação antes de oferecer qualquer nova opção.", rules.Content.SuggestedApproach);
    }
}
