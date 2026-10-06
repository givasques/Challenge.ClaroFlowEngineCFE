using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;

/// <summary>
/// Resumo por regras (FASE 4.4, A.5). Determinístico, sem chamada externa. É o padrão sem chave e o fallback da IA,
/// então precisa ser útil por si só: as frases saem das contagens e dos rótulos do retrato.
/// </summary>
public class RuleBasedSummaryProvider : ICustomerSummaryProvider
{
    public const string ProviderName = "rules";

    private const int MaxSummaryLength = 400;
    private const int MaxPointLength = 160;
    private const int MaxApproachLength = 250;
    private const int MaxAttentionPoints = 4;
    private const int RecentDays = 30;
    private const int StoppedMinutes = 15;

    private static readonly string EscalatedLabel = JourneyStatus.Label(JourneyStatus.Escalated);
    private static readonly string AbandonedLabel = JourneyStatus.Label(JourneyStatus.Abandoned);
    private static readonly string ExpiredLabel = JourneyStatus.Label(JourneyStatus.Expired);
    private static readonly string OpenLabel = JourneyStatus.Label(JourneyStatus.Open);
    private static readonly string ChangePlanLabel = ClaroFlowEngine.Api.Modules.Panel.PanelLabels.Intent("change_plan");
    private static readonly string CriticalUrgencyLabel = OpportunityUrgency.Label(OpportunityUrgency.Critical);
    private static readonly string NewOpportunityLabel = OpportunityStatus.Label(OpportunityStatus.New);
    private static readonly string ContactedOpportunityLabel = OpportunityStatus.Label(OpportunityStatus.Contacted);

    public string Name => ProviderName;

    public Task<CustomerSummaryResult> GenerateAsync(CustomerPortrait portrait, CancellationToken cancellationToken)
    {
        var content = Build(portrait);
        return Task.FromResult(new CustomerSummaryResult(content, "rules", null));
    }

    /// <summary>Monta o resumo por regras. Cliente sem jornada recebe o texto fixo e nenhum ponto nem sugestão.</summary>
    internal static CustomerSummaryContent Build(CustomerPortrait portrait)
    {
        if (portrait.Counts.Total == 0)
            return new CustomerSummaryContent("Ainda não há atendimentos registrados para este cliente.", [], null);

        var summary = Truncate(BuildSummary(portrait), MaxSummaryLength);
        var points = BuildAttentionPoints(portrait)
            .Take(MaxAttentionPoints)
            .Select(point => Truncate(point, MaxPointLength))
            .ToList();
        var approach = Truncate(BuildApproach(portrait), MaxApproachLength);

        return new CustomerSummaryContent(summary, points, approach);
    }

    private static string BuildSummary(CustomerPortrait portrait)
    {
        var total = portrait.Counts.Total;
        var sentence = total == 1
            ? "Cliente com 1 atendimento registrado"
            : $"Cliente com {total} atendimentos registrados";

        var topIntent = portrait.Journeys
            .GroupBy(j => j.Intent)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        if (topIntent is not null)
        {
            var qualifier = topIntent.Count() > 1 ? "a maioria sobre" : "sobre";
            sentence += $", {qualifier} {LowerFirst(topIntent.Key)}";
        }
        sentence += ".";

        var last = portrait.Journeys.FirstOrDefault();
        if (last is not null && last.Status == EscalatedLabel)
        {
            var area = last.EscalationArea is null ? "outra área" : last.EscalationArea;
            sentence += $" O último foi escalado para {area} {last.LastActivityAgo}.";
        }

        return sentence;
    }

    private static List<string> BuildAttentionPoints(CustomerPortrait portrait)
    {
        var points = new List<string>();

        // 1. Escalada sem desfecho.
        foreach (var journey in portrait.Journeys.Where(j => j.Status == EscalatedLabel && j.Resolution is null))
        {
            var area = journey.EscalationArea ?? "outra área";
            points.Add($"Jornada escalada para {area} {journey.LastActivityAgo}, sem desfecho registrado.");
        }

        // 2. Contestação nos últimos 30 dias.
        foreach (var dispute in portrait.Disputes.Where(d => d.DaysSinceStart <= RecentDays))
            points.Add($"Contestou a fatura de {dispute.ReferenceMonth} ({dispute.Amount}): {LowerFirst(dispute.Reason)}.");

        // 3. Abandono ou expiração recente.
        foreach (var journey in portrait.Journeys.Where(j =>
                     (j.Status == AbandonedLabel || j.Status == ExpiredLabel) && j.DaysSinceStart <= RecentDays))
            points.Add($"{journey.Intent} {LowerFirst(journey.Status)} {journey.StartedAgo}, na etapa {LowerFirst(journey.CurrentStep)}.");

        // 4. Oportunidade crítica aberta.
        foreach (var opportunity in portrait.Opportunities.Where(o =>
                     o.Urgency == CriticalUrgencyLabel
                     && (o.Status == NewOpportunityLabel || o.Status == ContactedOpportunityLabel)))
            points.Add($"Oportunidade crítica aberta: {LowerFirst(opportunity.Category)}.");

        // 5. Jornada aberta parada.
        foreach (var journey in portrait.Journeys.Where(j => j.Status == OpenLabel && j.MinutesSinceLastActivity >= StoppedMinutes))
            points.Add($"Jornada aberta parada {journey.LastActivityAgo}, na etapa {LowerFirst(journey.CurrentStep)}.");

        return points;
    }

    private static string BuildApproach(CustomerPortrait portrait)
    {
        if (portrait.Journeys.Any(j => j.Status == EscalatedLabel && j.Resolution is null))
            return "Confirmar o andamento da escalação antes de oferecer qualquer nova opção.";

        if (portrait.Journeys.Any(j => j.Intent == ChangePlanLabel && (j.Status == AbandonedLabel || j.Status == ExpiredLabel)))
            return "Retomar a troca de plano de onde parou.";

        return "Atendimento padrão.";
    }

    private static string LowerFirst(string text) =>
        text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
