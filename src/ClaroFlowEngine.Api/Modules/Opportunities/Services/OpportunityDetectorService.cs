using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Modules.Opportunities.Services;

/// <summary>
/// As 4 regras de detecção de oportunidades (FASE 3.6, item A.3). Dataset pequeno no protótipo —
/// cada regra busca o recorte relevante e agrupa/filtra em memória, em vez de traduzir DISTINCT ON,
/// CTEs e HAVING (Postgres-specific ou pouco naturais em LINQ) para SQL bruto.
/// </summary>
public class OpportunityDetectorService : IOpportunityDetectorService
{
    private readonly CfeDbContext _db;

    public OpportunityDetectorService(CfeDbContext db) => _db = db;

    public async Task<Dictionary<string, int>> DetectAllAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Cada regra roda sequencialmente e salva antes da próxima, pra que uma regra não crie
        // duplicata que a próxima ainda não veria (irrelevante aqui, pois as categorias não se
        // sobrepõem, mas mantém o SaveChanges perto de onde os dados são montados).
        var result = new Dictionary<string, int>
        {
            [OpportunityCategory.AbandonedPlanChange] = await DetectAbandonedPlanChangeAsync(now, cancellationToken),
            [OpportunityCategory.AbandonedDispute] = await DetectAbandonedDisputeAsync(now, cancellationToken),
            [OpportunityCategory.ActiveEngagedCustomer] = await DetectActiveEngagedCustomerAsync(now, cancellationToken),
            [OpportunityCategory.InactiveCustomer] = await DetectInactiveCustomerAsync(now, cancellationToken),
        };

        return result;
    }

    /// <summary>Regra 1: jornada de troca de plano abandonada/expirada nos últimos 30 dias, sem troca concluída depois.</summary>
    private async Task<int> DetectAbandonedPlanChangeAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string category = OpportunityCategory.AbandonedPlanChange;
        var windowStart = now - TimeSpan.FromDays(30);

        var candidates = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Intent == "change_plan"
                && (j.Status == JourneyStatus.Abandoned || j.Status == JourneyStatus.Expired)
                && j.UpdatedAt > windowStart)
            .Select(j => new { j.Id, j.CustomerId, j.Payload, j.UpdatedAt, j.CurrentStep })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return 0;

        // Mais recente por cliente (equivalente ao DISTINCT ON (customer_id) ... ORDER BY updated_at DESC).
        var latestPerCustomer = candidates
            .GroupBy(c => c.CustomerId)
            .Select(g => g.OrderByDescending(c => c.UpdatedAt).First())
            .ToList();

        var customerIds = latestPerCustomer.Select(c => c.CustomerId).ToList();

        var excludedByExistingOpportunity = await GetCustomersWithActiveOpportunityAsync(customerIds, category, cancellationToken);

        var laterConcludedByCustomer = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => customerIds.Contains(j.CustomerId) && j.Intent == "change_plan" && j.Status == JourneyStatus.Concluded)
            .Select(j => new { j.CustomerId, j.UpdatedAt })
            .ToListAsync(cancellationToken);

        var activePlanByCustomer = await GetActivePlanCodesAsync(customerIds, cancellationToken);

        var created = 0;
        foreach (var candidate in latestPerCustomer)
        {
            if (excludedByExistingOpportunity.Contains(candidate.CustomerId)) continue;
            if (laterConcludedByCustomer.Any(c => c.CustomerId == candidate.CustomerId && c.UpdatedAt > candidate.UpdatedAt)) continue;

            var metadata = new Dictionary<string, object>
            {
                ["abandoned_at_step"] = candidate.CurrentStep,
                ["plan_of_interest"] = candidate.Payload.GetValueOrDefault("selected_plan_code")?.ToString() ?? "",
                ["original_plan"] = activePlanByCustomer.GetValueOrDefault(candidate.CustomerId, ""),
            };

            _db.Opportunities.Add(BuildOpportunity(candidate.CustomerId, candidate.Id, category, now, metadata));
            created++;
        }

        if (created > 0) await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>Regra 3: jornada de contestação abandonada/expirada nos últimos 7 dias (janela curta = urgência crítica).</summary>
    private async Task<int> DetectAbandonedDisputeAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string category = OpportunityCategory.AbandonedDispute;
        var windowStart = now - TimeSpan.FromDays(7);

        var candidates = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Intent == "dispute_charge"
                && (j.Status == JourneyStatus.Abandoned || j.Status == JourneyStatus.Expired)
                && j.UpdatedAt > windowStart)
            .Select(j => new { j.Id, j.CustomerId, j.Payload, j.UpdatedAt, j.CurrentStep })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0) return 0;

        var latestPerCustomer = candidates
            .GroupBy(c => c.CustomerId)
            .Select(g => g.OrderByDescending(c => c.UpdatedAt).First())
            .ToList();

        var customerIds = latestPerCustomer.Select(c => c.CustomerId).ToList();
        var excluded = await GetCustomersWithActiveOpportunityAsync(customerIds, category, cancellationToken);

        var created = 0;
        foreach (var candidate in latestPerCustomer)
        {
            if (excluded.Contains(candidate.CustomerId)) continue;

            var metadata = new Dictionary<string, object>
            {
                ["invoice_id"] = candidate.Payload.GetValueOrDefault("invoice_id")?.ToString() ?? "",
                ["dispute_reason"] = candidate.Payload.GetValueOrDefault("dispute_reason")?.ToString() ?? "",
                ["abandoned_at_step"] = candidate.CurrentStep,
            };

            _db.Opportunities.Add(BuildOpportunity(candidate.CustomerId, candidate.Id, category, now, metadata));
            created++;
        }

        if (created > 0) await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>Regra 4: 3+ jornadas concluídas nos últimos 30 dias — engajamento acima da média (candidato a upsell).</summary>
    private async Task<int> DetectActiveEngagedCustomerAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string category = OpportunityCategory.ActiveEngagedCustomer;
        var windowStart = now - TimeSpan.FromDays(30);

        var concludedJourneys = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Status == JourneyStatus.Concluded && j.UpdatedAt > windowStart)
            .Select(j => new { j.Id, j.CustomerId, j.Intent, j.UpdatedAt })
            .ToListAsync(cancellationToken);

        var byCustomer = concludedJourneys
            .GroupBy(j => j.CustomerId)
            .Where(g => g.Count() >= 3)
            .ToList();

        if (byCustomer.Count == 0) return 0;

        var customerIds = byCustomer.Select(g => g.Key).ToList();
        var excluded = await GetCustomersWithActiveOpportunityAsync(customerIds, category, cancellationToken);

        var created = 0;
        foreach (var group in byCustomer)
        {
            if (excluded.Contains(group.Key)) continue;

            var mostRecent = group.OrderByDescending(j => j.UpdatedAt).First();
            var metadata = new Dictionary<string, object>
            {
                ["recent_journey_count"] = group.Count(),
                ["intents_used"] = group.Select(j => j.Intent).Distinct().ToList(),
            };

            _db.Opportunities.Add(BuildOpportunity(group.Key, mostRecent.Id, category, now, metadata));
            created++;
        }

        if (created > 0) await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>
    /// Regra 5: cliente com 2+ jornadas nos últimos 180 dias, mas sem nenhuma interação nos últimos 60
    /// (e a última atividade não é mais antiga que 180 dias — senão o cliente já "sumiu" há tempo demais
    /// pra uma reativação fazer sentido nesta janela).
    /// </summary>
    private async Task<int> DetectInactiveCustomerAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string category = OpportunityCategory.InactiveCustomer;
        var windowStart = now - TimeSpan.FromDays(180);
        var recentCutoff = now - TimeSpan.FromDays(60);

        var journeys180d = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.UpdatedAt > windowStart)
            .Select(j => new { j.Id, j.CustomerId, j.UpdatedAt })
            .ToListAsync(cancellationToken);

        var activity = journeys180d
            .GroupBy(j => j.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                TotalJourneys = g.Count(),
                LastActivity = g.Max(j => j.UpdatedAt),
                LastJourneyId = g.OrderByDescending(j => j.UpdatedAt).First().Id,
            })
            .Where(a => a.TotalJourneys >= 2 && a.LastActivity < recentCutoff)
            .ToList();

        if (activity.Count == 0) return 0;

        var customerIds = activity.Select(a => a.CustomerId).ToList();
        var excluded = await GetCustomersWithActiveOpportunityAsync(customerIds, category, cancellationToken);

        var created = 0;
        foreach (var a in activity)
        {
            if (excluded.Contains(a.CustomerId)) continue;

            var metadata = new Dictionary<string, object>
            {
                ["last_activity_at"] = a.LastActivity,
                ["days_since_activity"] = (int)(now - a.LastActivity).TotalDays,
                ["total_journeys_last_180d"] = a.TotalJourneys,
            };

            _db.Opportunities.Add(BuildOpportunity(a.CustomerId, a.LastJourneyId, category, now, metadata));
            created++;
        }

        if (created > 0) await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    private static Opportunity BuildOpportunity(
        Guid customerId, Guid triggeringJourneyId, string category, DateTime now, Dictionary<string, object> metadata) => new()
    {
        CustomerId = customerId,
        Category = category,
        Urgency = OpportunityCategory.Urgency(category),
        Status = OpportunityStatus.New,
        TriggeringJourneyId = triggeringJourneyId,
        Metadata = metadata,
        DetectedAt = now,
        ValidUntil = now.AddDays(OpportunityCategory.ValidityDaysFor(category)),
    };

    /// <summary>Clientes que já têm oportunidade ativa (new/contacted) da mesma categoria — evita duplicata (A.3).</summary>
    private async Task<HashSet<Guid>> GetCustomersWithActiveOpportunityAsync(
        List<Guid> customerIds, string category, CancellationToken cancellationToken)
    {
        var existing = await _db.Opportunities
            .AsNoTracking()
            .Where(o => customerIds.Contains(o.CustomerId) && o.Category == category
                && (o.Status == OpportunityStatus.New || o.Status == OpportunityStatus.Contacted))
            .Select(o => o.CustomerId)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet();
    }

    private async Task<Dictionary<Guid, string>> GetActivePlanCodesAsync(List<Guid> customerIds, CancellationToken cancellationToken)
    {
        var plans = await _db.CustomerPlans
            .AsNoTracking()
            .Where(cp => customerIds.Contains(cp.CustomerId) && cp.Active)
            .Select(cp => new { cp.CustomerId, cp.Plan.Code })
            .ToListAsync(cancellationToken);

        return plans.ToDictionary(p => p.CustomerId, p => p.Code);
    }
}
