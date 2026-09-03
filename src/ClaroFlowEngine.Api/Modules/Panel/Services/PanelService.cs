using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Panel.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Modules.Panel.Services;

/// <summary>
/// Endpoints agregados para o menu lateral do painel do atendente (FASE 3.2) — jornadas ativas e
/// métricas operacionais — e ações de fechamento/escalação de jornada pelo atendente (FASE 3.5).
/// </summary>
public class PanelService : IPanelService
{
    private static readonly TimeSpan MetricsWindow = TimeSpan.FromDays(30);
    private const int MaxDescriptionLength = 500;

    private readonly CfeDbContext _db;
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly ICurrentChannelAccessor _currentChannel;

    public PanelService(CfeDbContext db, ITransitionRecorder transitionRecorder, ICurrentChannelAccessor currentChannel)
    {
        _db = db;
        _transitionRecorder = transitionRecorder;
        _currentChannel = currentChannel;
    }

    public async Task<ActiveJourneysResponse> GetActiveJourneysAsync(bool includeEscalated, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Padrão do MVP (A.8): só 'open'. include_escalated=true é opcional, pensado pra uma futura
        // aba separada no painel — não muda o comportamento default de /journeys/active nem da
        // contagem "Jornadas ativas" do menu lateral.
        var openJourneys = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Status == JourneyStatus.Open || (includeEscalated && j.Status == JourneyStatus.Escalated))
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
                j.Status,
            })
            .ToListAsync(cancellationToken);

        if (openJourneys.Count == 0)
            return new ActiveJourneysResponse([], 0);

        var journeyIds = openJourneys.Select(j => j.Id).ToList();

        // Canal atual = canal da transição mais recente (mesma regra usada na timeline do painel);
        // dataset pequeno no protótipo, resolvido em memória em vez de uma subquery correlacionada por jornada.
        var latestChannelByJourney = await _db.JourneyTransitions
            .AsNoTracking()
            .Where(t => t.JourneyContextId != null && journeyIds.Contains(t.JourneyContextId.Value))
            .Select(t => new { t.JourneyContextId, t.Channel, t.OccurredAt })
            .ToListAsync(cancellationToken);

        var currentChannelById = latestChannelByJourney
            .GroupBy(t => t.JourneyContextId!.Value)
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
                (int)(now - j.CreatedAt).TotalMinutes,
                j.Status);
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

    public async Task<ConcludeJourneyResponse> ConcludeJourneyAsync(
        Guid journeyId, ConcludeJourneyRequest request, CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        if (!ResolutionCategory.IsValid(request.ResolutionCategory))
            throw new ValidationException("invalid_resolution_category", "resolution_category inválida.");
        ValidateDescription(request.Description);

        var journey = await GetOpenJourneyOrThrowAsync(journeyId, cancellationToken);

        var closedAt = DateTime.UtcNow;
        journey.Status = JourneyStatus.Concluded;
        journey.ClosedAt = closedAt;
        journey.UpdatedAt = closedAt;
        journey.Payload["resolution_category"] = request.ResolutionCategory;
        if (!string.IsNullOrWhiteSpace(request.Description))
            journey.Payload["resolution_description"] = request.Description;

        var label = ResolutionCategory.Label(request.ResolutionCategory);
        _transitionRecorder.Record(journey.Id, Channels.Panel, TransitionEventTypes.JourneyConcludedByAgent,
            $"Jornada concluída pelo atendente — {label}",
            new { resolution_category = request.ResolutionCategory, description = request.Description });

        await _db.SaveChangesAsync(cancellationToken);

        return new ConcludeJourneyResponse(journey.Id, journey.Status, closedAt, request.ResolutionCategory, label);
    }

    public async Task<EscalateJourneyResponse> EscalateJourneyAsync(
        Guid journeyId, EscalateJourneyRequest request, CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        if (!EscalationArea.IsValid(request.EscalationArea))
            throw new ValidationException("invalid_escalation_area", "escalation_area inválida.");
        ValidateDescription(request.Description);

        var journey = await GetOpenJourneyOrThrowAsync(journeyId, cancellationToken);

        var escalatedAt = DateTime.UtcNow;
        journey.Status = JourneyStatus.Escalated;
        journey.UpdatedAt = escalatedAt;
        journey.EscalatedAt = escalatedAt;
        // Sem ClosedAt de propósito (A.5): a jornada não fechou, só foi transferida — continua "viva" no CFE.
        journey.Payload["escalation_area"] = request.EscalationArea;
        if (!string.IsNullOrWhiteSpace(request.Description))
            journey.Payload["escalation_description"] = request.Description;

        var label = EscalationArea.Label(request.EscalationArea);
        _transitionRecorder.Record(journey.Id, Channels.Panel, TransitionEventTypes.JourneyEscalated,
            $"Jornada escalada para {label}",
            new { escalation_area = request.EscalationArea, description = request.Description });

        await _db.SaveChangesAsync(cancellationToken);

        return new EscalateJourneyResponse(journey.Id, journey.Status, escalatedAt, request.EscalationArea, label);
    }

    private void EnsurePanelChannel()
    {
        if (_currentChannel.Channel != Channels.Panel)
            throw new ForbiddenException("channel_not_allowed", "Esta ação só pode ser executada pelo painel do atendente.");
    }

    private static void ValidateDescription(string? description)
    {
        if (description is { Length: > MaxDescriptionLength })
            throw new ValidationException("invalid_description", $"description deve ter no máximo {MaxDescriptionLength} caracteres.");
    }

    private async Task<JourneyContext> GetOpenJourneyOrThrowAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var journey = await _db.JourneyContexts.FirstOrDefaultAsync(j => j.Id == journeyId, cancellationToken)
            ?? throw new NotFoundException("journey_not_found", $"Jornada {journeyId} não encontrada.");

        if (journey.Status != JourneyStatus.Open)
            throw new ConflictException("journey_not_open", $"Jornada está no estado '{journey.Status}'.", new { status = journey.Status });

        return journey;
    }
}
