using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Modules.Panel.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Modules.Panel.Services;

/// <summary>Endpoints agregados para o menu lateral do painel do atendente (FASE 3.2) — jornadas ativas e métricas operacionais.</summary>
public class PanelService : IPanelService
{
    private static readonly TimeSpan MetricsWindow = TimeSpan.FromDays(30);

    private readonly CfeDbContext _db;

    public PanelService(CfeDbContext db) => _db = db;

    public async Task<ActiveJourneysResponse> GetActiveJourneysAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var openJourneys = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Status == JourneyStatus.Open)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new
            {
                j.Id,
                j.CustomerId,
                CustomerFullName = j.Customer.FullName,
                CustomerCpf = j.Customer.Cpf,
                j.Intent,
                j.OriginChannel,
                j.CreatedAt,
                j.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        if (openJourneys.Count == 0)
            return new ActiveJourneysResponse([], 0);

        var journeyIds = openJourneys.Select(j => j.Id).ToList();

        // Canal atual = canal da transição mais recente (mesma regra usada na timeline do painel);
        // dataset pequeno no protótipo, resolvido em memória em vez de uma subquery correlacionada por jornada.
        var latestChannelByJourney = await _db.JourneyTransitions
            .AsNoTracking()
            .Where(t => journeyIds.Contains(t.JourneyContextId))
            .Select(t => new { t.JourneyContextId, t.Channel, t.OccurredAt })
            .ToListAsync(cancellationToken);

        var currentChannelById = latestChannelByJourney
            .GroupBy(t => t.JourneyContextId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.OccurredAt).First().Channel);

        var journeys = openJourneys.Select(j =>
        {
            var currentChannel = currentChannelById.GetValueOrDefault(j.Id, j.OriginChannel);
            return new ActiveJourneyDto(
                j.Id,
                new ActiveJourneyCustomerDto(j.CustomerId, j.CustomerFullName, j.CustomerCpf),
                j.Intent,
                PanelLabels.Intent(j.Intent),
                j.OriginChannel,
                PanelLabels.Channel(j.OriginChannel),
                currentChannel,
                PanelLabels.Channel(currentChannel),
                j.CreatedAt,
                j.UpdatedAt,
                (int)(now - j.CreatedAt).TotalMinutes);
        }).ToList();

        return new ActiveJourneysResponse(journeys, journeys.Count);
    }

    public async Task<MetricsSummaryResponse> GetMetricsSummaryAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var windowStart = now - MetricsWindow;
        // DateOnly.ToDateTime produz Kind=Unspecified, que o Npgsql rejeita para timestamptz — precisa ser
        // forçado a Utc explicitamente (todas as colunas de data do CFE são timestamptz em UTC).
        var todayStartUtc = DateTime.SpecifyKind(DateOnly.FromDateTime(now).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var tomorrowStartUtc = todayStartUtc.AddDays(1);

        var tmaMedianSeconds = await ComputeTmaMedianSecondsAsync(windowStart, cancellationToken);

        var journeysToday = await _db.JourneyContexts
            .AsNoTracking()
            .CountAsync(j => j.CreatedAt >= todayStartUtc && j.CreatedAt < tomorrowStartUtc, cancellationToken);

        var closedInWindow = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.CreatedAt >= windowStart
                && (j.Status == JourneyStatus.Concluded || j.Status == JourneyStatus.Abandoned || j.Status == JourneyStatus.Expired))
            .Select(j => j.Status)
            .ToListAsync(cancellationToken);

        int? conclusionRatePercent = closedInWindow.Count == 0
            ? null
            : (int)Math.Round(closedInWindow.Count(s => s == JourneyStatus.Concluded) * 100.0 / closedInWindow.Count);

        var mostUsedChannel = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.CreatedAt >= windowStart)
            .GroupBy(j => j.OriginChannel)
            .Select(g => new { Channel = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        var topChannel = mostUsedChannel
            .OrderByDescending(g => g.Total)
            .ThenBy(g => g.Channel, StringComparer.Ordinal)
            .Select(g => g.Channel)
            .FirstOrDefault();

        return new MetricsSummaryResponse(
            tmaMedianSeconds,
            PanelLabels.TmaLabel(tmaMedianSeconds),
            journeysToday,
            conclusionRatePercent,
            topChannel,
            topChannel is null ? null : PanelLabels.Channel(topChannel),
            new MetricsPeriodDto(windowStart, now, (int)MetricsWindow.TotalDays));
    }

    /// <summary>
    /// Mediana calculada em memória (dataset pequeno no protótipo) — EF Core/Npgsql não traduz
    /// PERCENTILE_CONT para LINQ, e uma FromSqlRaw só pra isso não compensa a complexidade aqui.
    /// </summary>
    private async Task<long?> ComputeTmaMedianSecondsAsync(DateTime windowStart, CancellationToken cancellationToken)
    {
        var durations = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Status == JourneyStatus.Concluded && j.ClosedAt != null && j.ClosedAt >= windowStart)
            .Select(j => (long)(j.ClosedAt!.Value - j.CreatedAt).TotalSeconds)
            .ToListAsync(cancellationToken);

        if (durations.Count == 0) return null;

        durations.Sort();
        var mid = durations.Count / 2;
        return durations.Count % 2 == 0
            ? (durations[mid - 1] + durations[mid]) / 2
            : durations[mid];
    }
}
