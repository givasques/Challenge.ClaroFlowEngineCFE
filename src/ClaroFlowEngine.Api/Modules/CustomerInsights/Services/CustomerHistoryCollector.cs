using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;
using ClaroFlowEngine.Api.Modules.Panel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Services;

public interface ICustomerHistoryCollector
{
    Task<CollectedHistory> CollectAsync(Guid customerId, CancellationToken cancellationToken);
}

/// <summary>Retrato que vai ao provedor e a impressão digital dos dados que o originaram (cache, FASE 4.4, A.7).</summary>
public sealed record CollectedHistory(CustomerPortrait Portrait, string Fingerprint);

/// <summary>
/// Monta o retrato minimizado do cliente (FASE 4.4, A.3). Consulta as tabelas com projeção explícita: nenhum campo
/// pessoal é lido para o retrato. Não reaproveita a exportação da 4.3 (P44 do guia): aquela carrega dado pessoal.
/// </summary>
public class CustomerHistoryCollector : ICustomerHistoryCollector
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private const string DisputeIntent = "dispute_charge";
    private const int MaxOpportunities = 10;

    private readonly CfeDbContext _db;
    private readonly AiSummaryOptions _options;

    public CustomerHistoryCollector(CfeDbContext db, IOptions<AiSummaryOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<CollectedHistory> CollectAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var journeys = await _db.JourneyContexts.AsNoTracking()
            .Where(j => j.CustomerId == customerId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync(cancellationToken);

        var journeyIds = journeys.Select(j => j.Id).ToList();
        var transitions = await _db.JourneyTransitions.AsNoTracking()
            .Where(t => t.JourneyContextId != null && journeyIds.Contains(t.JourneyContextId.Value))
            .Select(t => new JourneyEvent(t.JourneyContextId!.Value, t.Channel, t.OccurredAt))
            .ToListAsync(cancellationToken);
        var eventsByJourney = transitions
            .GroupBy(e => e.JourneyId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.OccurredAt).ToList());

        var counts = new PortraitCounts(
            Total: journeys.Count,
            Open: journeys.Count(j => j.Status == JourneyStatus.Open),
            Concluded: journeys.Count(j => j.Status == JourneyStatus.Concluded),
            Abandoned: journeys.Count(j => j.Status == JourneyStatus.Abandoned),
            Expired: journeys.Count(j => j.Status == JourneyStatus.Expired),
            Escalated: journeys.Count(j => j.Status == JourneyStatus.Escalated));

        var portraitJourneys = journeys
            .Take(_options.MaxJourneys)
            .Select(j => ToPortraitJourney(j, eventsByJourney.GetValueOrDefault(j.Id) ?? [], now))
            .ToList();

        var customerPlans = await _db.CustomerPlans.AsNoTracking()
            .Include(cp => cp.Plan)
            .Where(cp => cp.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        var chosenPlanCodes = journeys
            .Select(j => PayloadText(j.Payload, "selected_plan_code"))
            .OfType<string>()
            .Distinct()
            .ToList();
        var chosenPlans = await _db.Plans.AsNoTracking()
            .Where(p => chosenPlanCodes.Contains(p.Code))
            .ToListAsync(cancellationToken);

        var currentPlanIds = customerPlans.Where(cp => cp.Active).Select(cp => cp.PlanId).ToHashSet();
        var plans = customerPlans
            .Where(cp => cp.Active)
            .Select(cp => new PortraitPlan(cp.Plan.Name, FormatPrice(cp.Plan.MonthlyPriceCents), Current: true))
            .Concat(chosenPlans
                .Where(p => !currentPlanIds.Contains(p.Id))
                .Select(p => new PortraitPlan(p.Name, FormatPrice(p.MonthlyPriceCents), Current: false)))
            .ToList();

        var disputeJourneys = journeys
            .Where(j => j.Intent == DisputeIntent && PayloadText(j.Payload, "invoice_id") is not null)
            .ToList();
        var invoiceIds = disputeJourneys
            .Select(j => ParseGuid(PayloadText(j.Payload, "invoice_id")))
            .OfType<Guid>()
            .ToList();
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.Id))
            .ToListAsync(cancellationToken);

        var disputes = new List<PortraitDispute>();
        foreach (var journey in disputeJourneys)
        {
            var invoiceId = ParseGuid(PayloadText(journey.Payload, "invoice_id"));
            var invoice = invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (invoice is null) continue;

            var reason = PayloadText(journey.Payload, "dispute_reason");
            disputes.Add(new PortraitDispute(
                reason is null ? "Motivo não informado" : DisputeReason.Label(reason),
                invoice.ReferenceMonth.ToDateTime(TimeOnly.MinValue).ToString("MMMM 'de' yyyy", PtBr),
                FormatPrice(invoice.TotalCents),
                DaysBetween(journey.CreatedAt, now)));
        }

        var opportunities = await _db.Opportunities.AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.DetectedAt)
            .Take(MaxOpportunities)
            .ToListAsync(cancellationToken);
        var portraitOpportunities = opportunities
            .Select(o => new PortraitOpportunity(
                OpportunityCategory.Label(o.Category),
                OpportunityUrgency.Label(o.Urgency),
                OpportunityStatus.Label(o.Status)))
            .ToList();

        var portrait = new CustomerPortrait(counts, portraitJourneys, plans, disputes, portraitOpportunities);
        var fingerprint = ComputeFingerprint(journeys, eventsByJourney, customerPlans, invoices, opportunities);
        return new CollectedHistory(portrait, fingerprint);
    }

    private static PortraitJourney ToPortraitJourney(JourneyContext journey, IReadOnlyList<JourneyEvent> events, DateTime now)
    {
        var channelsVisited = events
            .Select(e => e.Channel)
            .Prepend(journey.OriginChannel)
            .Where(channel => channel != Channels.Panel && channel != Channels.System)
            .Distinct()
            .Select(PanelLabels.Channel)
            .ToList();

        var resolution = PayloadText(journey.Payload, "resolution_category");
        var escalationArea = PayloadText(journey.Payload, "escalation_area");
        var disputeReason = PayloadText(journey.Payload, "dispute_reason");
        var end = journey.ClosedAt ?? now;

        return new PortraitJourney(
            Intent: PanelLabels.Intent(journey.Intent),
            Status: JourneyStatus.Label(journey.Status),
            OriginChannel: PanelLabels.Channel(journey.OriginChannel),
            ChannelsVisited: channelsVisited,
            CurrentStep: PanelLabels.CurrentStep(journey.CurrentStep),
            Resolution: resolution is null ? null : ResolutionCategory.Label(resolution),
            EscalationArea: escalationArea is null ? null : EscalationArea.Label(escalationArea),
            DisputeReason: disputeReason is null ? null : DisputeReason.Label(disputeReason),
            StartedAgo: RelativeAgo(journey.CreatedAt, now),
            DaysSinceStart: DaysBetween(journey.CreatedAt, now),
            Duration: FormatDuration(end - journey.CreatedAt),
            LastActivityAgo: RelativeAgo(journey.UpdatedAt, now),
            MinutesSinceLastActivity: Math.Max(0, (int)(now - journey.UpdatedAt).TotalMinutes));
    }

    /// <summary>
    /// Impressão digital dos fatos internos (ids, estados, datas de atividade e contagem de transições). Não usa o
    /// tempo corrido, então a passagem do tempo não invalida o cache; nova jornada ou nova transição, sim.
    /// Nunca é enviada ao provedor.
    /// </summary>
    private static string ComputeFingerprint(
        IReadOnlyList<JourneyContext> journeys,
        IReadOnlyDictionary<Guid, List<JourneyEvent>> eventsByJourney,
        IReadOnlyList<CustomerPlan> customerPlans,
        IReadOnlyList<Invoice> invoices,
        IReadOnlyList<Opportunity> opportunities)
    {
        var sb = new StringBuilder();
        foreach (var journey in journeys.OrderBy(j => j.Id))
        {
            var events = eventsByJourney.GetValueOrDefault(journey.Id) ?? [];
            var lastEventTicks = events.Count == 0 ? 0 : events[^1].OccurredAt.Ticks;
            sb.Append("j|").Append(journey.Id).Append('|').Append(journey.Status).Append('|')
              .Append(journey.CurrentStep).Append('|').Append(journey.UpdatedAt.Ticks).Append('|')
              .Append(events.Count).Append('|').Append(lastEventTicks).Append('\n');
        }
        foreach (var plan in customerPlans.OrderBy(cp => cp.Id))
            sb.Append("p|").Append(plan.Id).Append('|').Append(plan.Active).Append('\n');
        foreach (var invoice in invoices.OrderBy(i => i.Id))
            sb.Append("i|").Append(invoice.Id).Append('|').Append(invoice.TotalCents).Append('\n');
        foreach (var opportunity in opportunities.OrderBy(o => o.Id))
            sb.Append("o|").Append(opportunity.Id).Append('|').Append(opportunity.Status).Append('|')
              .Append(opportunity.Urgency).Append('\n');

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()))).ToLowerInvariant();
    }

    /// <summary>Texto de um campo do payload (chaves gravadas pelo bot e pelo painel). Trata JsonElement vindo do jsonb.</summary>
    internal static string? PayloadText(IDictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var value) || value is null) return null;
        return value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => value.ToString(),
        };
    }

    private static Guid? ParseGuid(string? text) => Guid.TryParse(text, out var id) ? id : null;

    private static string FormatPrice(int cents) => (cents / 100m).ToString("C", PtBr);

    private static int DaysBetween(DateTime then, DateTime now) => Math.Max(0, (int)(now - then).TotalDays);

    /// <summary>"há 3 dias", nunca data absoluta (A.3).</summary>
    internal static string RelativeAgo(DateTime then, DateTime now)
    {
        var span = now - then;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalMinutes < 1) return "agora mesmo";
        if (span.TotalMinutes < 60)
        {
            var minutes = (int)span.TotalMinutes;
            return minutes == 1 ? "há 1 minuto" : $"há {minutes} minutos";
        }
        if (span.TotalHours < 24)
        {
            var hours = (int)span.TotalHours;
            return hours == 1 ? "há 1 hora" : $"há {hours} horas";
        }
        var days = (int)span.TotalDays;
        return days == 1 ? "há 1 dia" : $"há {days} dias";
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min" : $"{(int)span.TotalHours}h {span.Minutes}min";
    }
}

internal readonly record struct JourneyEvent(Guid JourneyId, string Channel, DateTime OccurredAt);
